using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
// ReSharper disable InconsistentNaming

namespace CASCExplorer
{
    public class DB3Row
    {
        private readonly byte[] m_data;
        private readonly DB3Reader m_reader;

        public byte[] Data => m_data;

        public DB3Row(DB3Reader reader, byte[] data)
        {
            m_reader = reader;
            m_data = data;
        }

        public unsafe T GetField<T>(int offset)
        {
            fixed(byte *ptr = m_data)
            {
                object retVal;
                switch (Type.GetTypeCode(typeof(T)))
                {
                    case TypeCode.String:
                        var start = BitConverter.ToInt32(m_data, offset);
                        retVal = m_reader.StringTable.TryGetValue(start, out var str) ? str : string.Empty;
                        return (T)retVal;
                    case TypeCode.SByte:
                        retVal = ptr[offset];
                        return (T)retVal;
                    case TypeCode.Byte:
                        retVal = ptr[offset];
                        return (T)retVal;
                    case TypeCode.Int16:
                        retVal = *(short*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.UInt16:
                        retVal = *(ushort*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.Int32:
                        retVal = *(int*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.UInt32:
                        retVal = *(uint*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.Single:
                        retVal = *(float*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.Empty:
                    case TypeCode.Object:
                    case TypeCode.DBNull:
                    case TypeCode.Boolean:
                    case TypeCode.Char:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                    case TypeCode.DateTime:
                    default:
                        return default(T);
                }
            }
        }
    }

    public class DB3Reader : IEnumerable<KeyValuePair<int, DB3Row>>
    {
        private readonly int HeaderSize;
        private const uint DB3FmtSig = 0x33424457;          // WDB3
        private const uint DB4FmtSig = 0x34424457;          // WDB4

        public int RecordsCount { get; private set; }
        public int FieldsCount { get; private set; }
        public int RecordSize { get; private set; }
        public int StringTableSize { get; private set; }
        public int MinIndex { get; private set; }
        public int MaxIndex { get; private set; }

        public Dictionary<int, string> StringTable { get; private set; }

        private readonly SortedDictionary<int, DB3Row> m_index = new SortedDictionary<int, DB3Row>();

        public DB3Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public DB3Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                {
                    throw new InvalidDataException("DB3 file is corrupted!");
                }

                var magic = reader.ReadUInt32();

                if (magic != DB3FmtSig && magic != DB4FmtSig)
                {
                    throw new InvalidDataException("DB3 file is corrupted!");
                }

                // would be 56 if neither, but that is covered by previous check.
                HeaderSize = magic == DB3FmtSig ? 48 : 52;

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                // ReSharper disable UnusedVariable
                var _tableHash = reader.ReadUInt32();
                var _build = reader.ReadUInt32();
                var _unk1 = reader.ReadUInt32(); // timemodified
                var _MinId = reader.ReadInt32();
                var _MaxId = reader.ReadInt32();
                var _locale = reader.ReadInt32();
                var CopyTableSize = reader.ReadInt32();

                if (magic == DB4FmtSig)
                {
                    var _metaFlags = reader.ReadInt32();
                }
                // ReSharper restore UnusedVariable

                var stringTableStart = HeaderSize + RecordsCount * RecordSize;
                var stringTableEnd = stringTableStart + StringTableSize;

                // Index table
                int[] m_indexes = null;
                var hasIndex = stringTableEnd + CopyTableSize < reader.BaseStream.Length;

                if (hasIndex)
                {
                    reader.BaseStream.Position = stringTableEnd;

                    m_indexes = new int[RecordsCount];

                    for (var i = 0; i < RecordsCount; i++)
                        m_indexes[i] = reader.ReadInt32();
                }

                // Records table
                reader.BaseStream.Position = HeaderSize;

                for (var i = 0; i < RecordsCount; i++)
                {
                    var recordBytes = reader.ReadBytes(RecordSize);

                    if (hasIndex)
                    {
                        var newRecordBytes = new byte[RecordSize + 4];

                        Array.Copy(BitConverter.GetBytes(m_indexes[i]), newRecordBytes, 4);
                        Array.Copy(recordBytes, 0, newRecordBytes, 4, recordBytes.Length);

                        m_index.Add(m_indexes[i], new DB3Row(this, newRecordBytes));
                    }
                    else
                    {
                        m_index.Add(BitConverter.ToInt32(recordBytes, 0), new DB3Row(this, recordBytes));
                    }
                }

                // Strings table
                reader.BaseStream.Position = stringTableStart;

                StringTable = new Dictionary<int, string>();

                while (reader.BaseStream.Position != stringTableEnd)
                {
                    var index = (int)reader.BaseStream.Position - stringTableStart;
                    StringTable[index] = reader.ReadCString();
                }

                // Copy index table
                long copyTablePos = stringTableEnd + (hasIndex ? 4 * RecordsCount : 0);

                if (copyTablePos != reader.BaseStream.Length && CopyTableSize != 0)
                {
                    reader.BaseStream.Position = copyTablePos;

                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        var id = reader.ReadInt32();
                        var idcopy = reader.ReadInt32();

                        RecordsCount++;

                        var copyRow = m_index[idcopy];
                        var newRowData = new byte[copyRow.Data.Length];
                        Array.Copy(copyRow.Data, newRowData, newRowData.Length);
                        Array.Copy(BitConverter.GetBytes(id), newRowData, 4);

                        m_index.Add(id, new DB3Row(this, newRowData));
                    }
                }
            }
        }

        public bool HasRow(int index)
        {
            return m_index.ContainsKey(index);
        }

        public DB3Row GetRow(int index)
        {
            m_index.TryGetValue(index, out var row);
            return row;
        }

        public IEnumerator<KeyValuePair<int, DB3Row>> GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_index.GetEnumerator();
        }
    }
}
