using System.Collections;
using System.Text;

namespace CascLibCore;

public class DB5Row(DB5Reader reader, byte[] data) {
    public byte[] Data { get; } = data;

    public T GetField<T>(int field, int arrayIndex = 0) {
        var meta = reader.Meta[field];

        if (meta.Bits != 0x00 && meta.Bits != 0x08 && meta.Bits != 0x10 && meta.Bits != 0x18 && meta.Bits != -32) {
            throw new Exception("Unknown meta.Flags");
        }

        var bytesCount = (32 - meta.Bits) >> 3;

        var code = Type.GetTypeCode(typeof(T));

        object? value = null;

        switch (code) {
            case TypeCode.Byte:
                if (meta.Bits != 0x18) {
                    throw new Exception("TypeCode.Byte Unknown meta.Bits");
                }

                value = Data[meta.Offset + bytesCount * arrayIndex];
                break;
            case TypeCode.SByte:
                if (meta.Bits != 0x18) {
                    throw new Exception("TypeCode.SByte Unknown meta.Bits");
                }

                value = (sbyte)Data[meta.Offset + bytesCount * arrayIndex];
                break;
            case TypeCode.Int16:
                if (meta.Bits != 0x10) {
                    throw new Exception("TypeCode.Int16 Unknown meta.Bits");
                }

                value = BitConverter.ToInt16(Data, meta.Offset + bytesCount * arrayIndex);
                break;
            case TypeCode.UInt16:
                if (meta.Bits != 0x10) {
                    throw new Exception("TypeCode.UInt16 Unknown meta.Bits");
                }

                value = BitConverter.ToUInt16(Data, meta.Offset + bytesCount * arrayIndex);
                break;
            case TypeCode.Int32:
                var b1 = new byte[4];
                Array.Copy(Data, meta.Offset + bytesCount * arrayIndex, b1, 0, bytesCount);
                value = BitConverter.ToInt32(b1, 0);
                break;
            case TypeCode.UInt32:
                var b2 = new byte[4];
                Array.Copy(Data, meta.Offset + bytesCount * arrayIndex, b2, 0, bytesCount);
                value = BitConverter.ToUInt32(b2, 0);
                break;
            case TypeCode.Int64:
                var b3 = new byte[8];
                Array.Copy(Data, meta.Offset + bytesCount * arrayIndex, b3, 0, bytesCount);
                value = BitConverter.ToInt64(b3, 0);
                break;
            case TypeCode.UInt64:
                var b4 = new byte[8];
                Array.Copy(Data, meta.Offset + bytesCount * arrayIndex, b4, 0, bytesCount);
                value = BitConverter.ToUInt64(b4, 0);
                break;
            case TypeCode.String:
                if (meta.Bits != 0x00) {
                    throw new Exception("TypeCode.String Unknown meta.Bits");
                }

                var b5 = new byte[4];
                Array.Copy(Data, meta.Offset + bytesCount * arrayIndex, b5, 0, bytesCount);
                int start = BitConverter.ToInt32(b5, 0), len = 0;
                while (reader.StringTable[start + len] != 0) {
                    len++;
                }

                value = Encoding.UTF8.GetString(reader.StringTable, start, len);
                break;
            case TypeCode.Single:
                if (meta.Bits != 0x00) {
                    throw new Exception("TypeCode.Single Unknown meta.Bits");
                }

                value = BitConverter.ToSingle(Data, meta.Offset + bytesCount * arrayIndex);
                break;
            case TypeCode.Empty:
            case TypeCode.Object:
            case TypeCode.DBNull:
            case TypeCode.Boolean:
            case TypeCode.Char:
            case TypeCode.Double:
            case TypeCode.Decimal:
            case TypeCode.DateTime:
            default:
                throw new Exception("Unknown TypeCode " + code);
        }

        return (T)value;
    }
}

public class DB2Meta {
    public short Bits { get; init; }
    public short Offset { get; init; }
}

public class DB5Reader : IEnumerable<KeyValuePair<int, DB5Row>> {
    private const int HeaderSize = 48;
    private const uint DB5FmtSig = 0x35424457; // WDB5

    private readonly Dictionary<int, DB5Row> m_index = [];

    public DB5Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

    public DB5Reader(Stream stream) {
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (reader.BaseStream.Length < HeaderSize) {
            throw new InvalidDataException("DB5 file is corrupted!");
        }

        var magic = reader.ReadUInt32();

        if (magic != DB5FmtSig) {
            throw new InvalidDataException("DB5 file is corrupted!");
        }

        RecordsCount = reader.ReadInt32();
        FieldsCount = reader.ReadInt32();
        RecordSize = reader.ReadInt32();
        StringTableSize = reader.ReadInt32();

        var tableHash = reader.ReadUInt32();
        var layoutHash = reader.ReadUInt32();
        MinIndex = reader.ReadInt32();
        MaxIndex = reader.ReadInt32();
        var locale = reader.ReadInt32();
        var copyTableSize = reader.ReadInt32();
        int flags = reader.ReadUInt16();
        int idIndex = reader.ReadUInt16();

        var isSparse = (flags & 0x1) != 0;
        var hasIndex = (flags & 0x4) != 0;

        Meta = new DB2Meta[FieldsCount];

        for (var i = 0; i < Meta.Length; i++) {
            Meta[i] = new DB2Meta { Bits = reader.ReadInt16(),
                Offset = reader.ReadInt16(),
            };
        }

        var m_rows = new DB5Row[RecordsCount];

        for (var i = 0; i < RecordsCount; i++) {
            m_rows[i] = new DB5Row(this, reader.ReadBytes(RecordSize));
        }

        StringTable = reader.ReadBytes(StringTableSize);

        if (isSparse) {
            // code...
            throw new Exception("can't do sparse table");
        }

        if (hasIndex) {
            for (var i = 0; i < RecordsCount; i++) {
                var id = reader.ReadInt32();
                m_index[id] = m_rows[i];
            }
        }
        else {
            for (var i = 0; i < RecordsCount; i++) {
                var id = m_rows[i].Data.Skip(Meta[idIndex].Offset).Take((32 - Meta[idIndex].Bits) >> 3)
                    .Select((b, k) => b << (k * 8)).Sum();
                m_index[id] = m_rows[i];
            }
        }

        if (copyTableSize > 0) {
            var copyCount = copyTableSize / 8;

            for (var i = 0; i < copyCount; i++) {
                var newId = reader.ReadInt32();
                var oldId = reader.ReadInt32();

                m_index[newId] = m_index[oldId];
            }
        }
    }

    public int RecordsCount { get; }
    public int FieldsCount { get; }
    public int RecordSize { get; }
    public int StringTableSize { get; }
    public int MinIndex { get; }
    public int MaxIndex { get; }

    public byte[] StringTable { get; }

    public DB2Meta[] Meta { get; }

    public IEnumerator<KeyValuePair<int, DB5Row>> GetEnumerator() {
        return m_index.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return m_index.GetEnumerator();
    }

    public DB5Row? GetRow(int id) {
        return m_index.GetValueOrDefault(id);
    }

    public bool HasRow(int id) {
        return m_index.ContainsKey(id);
    }
}
