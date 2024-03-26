using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CASCExplorer
{
    internal class BLTEDecoderException : Exception
    {
        public BLTEDecoderException(string message) : base(message) { }

        public BLTEDecoderException(string fmt, params object[] args) : base(string.Format(fmt, args)) { }
    }

    internal class DataBlock
    {
        public int CompSize;
        public byte[] Data;
        public int DecompSize;
        public MD5Hash Hash;
    }

    internal class BLTEHandler : IDisposable
    {
        private const byte ENCRYPTION_SALSA20 = 0x53;
        private const byte ENCRYPTION_ARC4 = 0x41;
        private const int BLTE_MAGIC = 0x45544c42;
        private bool _leaveOpen;
        private readonly MD5 _md5 = MD5.Create();
        private MemoryStream _memStream;
        private readonly BinaryReader _reader;

        public BLTEHandler(Stream stream, MD5Hash md5)
        {
            _reader = new BinaryReader(stream, Encoding.ASCII, true);
            Parse(md5);
        }

        public void Dispose()
        {
            _reader.Dispose();

            if (!_leaveOpen)
            {
                _memStream.Dispose();
            }
        }

        public void ExtractToFile(string path, string name)
        {
            var fullPath = Path.Combine(path, name);
            var dir = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fileStream = File.Open(fullPath, FileMode.Create))
            {
                _memStream.Position = 0;
                _memStream.CopyTo(fileStream);
            }
        }

        public MemoryStream OpenFile(bool leaveOpen = false)
        {
            _leaveOpen = leaveOpen;
            _memStream.Position = 0;
            return _memStream;
        }

        private static void Decompress(byte[] data, Stream outStream)
        {
            // skip first 3 bytes (zlib)
            using (var ms = new MemoryStream(data, 3, data.Length - 3))
            using (var dfltStream = new DeflateStream(ms, CompressionMode.Decompress))
            {
                dfltStream.CopyTo(outStream);
            }
        }

        private static byte[] Decrypt(byte[] data, int index)
        {
            var keyNameSize = data[1];

            if (keyNameSize == 0 || keyNameSize != 8)
            {
                throw new BLTEDecoderException("keyNameSize == 0 || keyNameSize != 8");
            }

            var keyNameBytes = new byte[keyNameSize];
            Array.Copy(data, 2, keyNameBytes, 0, keyNameSize);

            var keyName = BitConverter.ToUInt64(keyNameBytes, 0);

            var IVSize = data[keyNameSize + 2];

            if (IVSize != 4 || IVSize > 0x10)
            {
                throw new BLTEDecoderException("IVSize != 4 || IVSize > 0x10");
            }

            var IVpart = new byte[IVSize];
            Array.Copy(data, keyNameSize + 3, IVpart, 0, IVSize);

            if (data.Length < IVSize + keyNameSize + 4)
            {
                throw new BLTEDecoderException("data.Length < IVSize + keyNameSize + 4");
            }

            var dataOffset = keyNameSize + IVSize + 3;

            var encType = data[dataOffset];

            if (encType != ENCRYPTION_SALSA20 && encType != ENCRYPTION_ARC4) // 'S' or 'A'
            {
                throw new BLTEDecoderException("encType != 0x53 && encType != 0x41");
            }

            dataOffset++;

            // expand to 8 bytes
            var IV = new byte[8];
            Array.Copy(IVpart, IV, IVpart.Length);

            // magic
            for (int shift = 0, i = 0; i < sizeof(int); shift += 8, i++)
            {
                IV[i] ^= (byte)((index >> shift) & 0xFF);
            }

            var key = KeyService.GetKey(keyName);

            if (key == null)
            {
                throw new BLTEDecoderException("unknown keyname {0:X16}", keyName);
            }

            if (encType == ENCRYPTION_SALSA20)
            {
                var decryptor = KeyService.SalsaInstance.CreateDecryptor(key, IV);

                return decryptor.TransformFinalBlock(data, dataOffset, data.Length - dataOffset);
            }

            // ARC4 ?
            throw new BLTEDecoderException("encType A not implemented");
        }

        private void HandleDataBlock(byte[] data, int index)
        {
            switch (data[0])
            {
                case 0x45: // E (encrypted)
                    var decrypted = Decrypt(data, index);
                    HandleDataBlock(decrypted, index);
                    break;
                case 0x46: // F (frame, recursive)
                    throw new BLTEDecoderException("DecoderFrame not implemented");
                case 0x4E: // N (not compressed)
                    _memStream.Write(data, 1, data.Length - 1);
                    break;
                case 0x5A: // Z (zlib compressed)
                    Decompress(data, _memStream);
                    break;
                default:
                    throw new BLTEDecoderException("unknown BLTE block type {0} (0x{1:X2})!", (char)data[0], data[0]);
            }
        }

        private void Parse(MD5Hash md5)
        {
            var size = (int)_reader.BaseStream.Length;

            if (size < 8)
            {
                throw new BLTEDecoderException("not enough data: {0}", 8);
            }

            var magic = _reader.ReadInt32();

            if (magic != BLTE_MAGIC)
            {
                throw new BLTEDecoderException("frame header mismatch (bad BLTE file)");
            }

            var headerSize = _reader.ReadInt32BE();

            if (CASCConfig.ValidateData)
            {
                var oldPos = _reader.BaseStream.Position;

                _reader.BaseStream.Position = 0;

                var newHash = _md5.ComputeHash(_reader.ReadBytes(headerSize > 0 ? headerSize : size));

                if (!md5.EqualsTo(newHash))
                {
                    throw new BLTEDecoderException("data corrupted");
                }

                _reader.BaseStream.Position = oldPos;
            }

            var numBlocks = 1;

            if (headerSize > 0)
            {
                if (size < 12)
                {
                    throw new BLTEDecoderException("not enough data: {0}", 12);
                }

                var fcbytes = _reader.ReadBytes(4);

                numBlocks = (fcbytes[1] << 16) | (fcbytes[2] << 8) | (fcbytes[3] << 0);

                if (fcbytes[0] != 0x0F || numBlocks == 0)
                {
                    throw new BLTEDecoderException("bad table format 0x{0:x2}, numBlocks {1}", fcbytes[0], numBlocks);
                }

                var frameHeaderSize = 24 * numBlocks + 12;

                if (headerSize != frameHeaderSize)
                {
                    throw new BLTEDecoderException("header size mismatch");
                }

                if (size < frameHeaderSize)
                {
                    throw new BLTEDecoderException("not enough data: {0}", frameHeaderSize);
                }
            }

            var blocks = new DataBlock[numBlocks];

            for (var i = 0; i < numBlocks; i++)
            {
                var block = new DataBlock();

                if (headerSize != 0)
                {
                    block.CompSize = _reader.ReadInt32BE();
                    block.DecompSize = _reader.ReadInt32BE();
                    block.Hash = _reader.Read<MD5Hash>();
                }
                else
                {
                    block.CompSize = size - 8;
                    block.DecompSize = size - 8 - 1;
                    block.Hash = default(MD5Hash);
                }

                blocks[i] = block;
            }

            _memStream = new MemoryStream(blocks.Sum(b => b.DecompSize));

            for (var i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];

                block.Data = _reader.ReadBytes(block.CompSize);

                if (!block.Hash.IsZeroed() && CASCConfig.ValidateData)
                {
                    var blockHash = _md5.ComputeHash(block.Data);

                    if (!block.Hash.EqualsTo(blockHash))
                    {
                        throw new BLTEDecoderException("MD5 mismatch");
                    }
                }

                HandleDataBlock(block.Data, i);
            }
        }
    }
}
