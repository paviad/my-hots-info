using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    public class DB2Row
    {
        private readonly DB2Reader m_reader;

        public DB2Row(DB2Reader reader, byte[] data)
        {
            m_reader = reader;
            Data = data;
        }

        public byte[] Data { get; }

        public T GetField<T>(int field)
        {
            object retVal;

            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.String:
                    int start = BitConverter.ToInt32(Data, field * 4), len = 0;
                    while (m_reader.StringTable[start + len] != 0)
                    {
                        len++;
                    }

                    retVal = Encoding.UTF8.GetString(m_reader.StringTable, start, len);
                    return (T)retVal;
                case TypeCode.Int32:
                    retVal = BitConverter.ToInt32(Data, field * 4);
                    return (T)retVal;
                case TypeCode.Single:
                    retVal = BitConverter.ToSingle(Data, field * 4);
                    return (T)retVal;
                case TypeCode.Empty:
                case TypeCode.Object:
                case TypeCode.DBNull:
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
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

    public class DB2Reader : IEnumerable<KeyValuePair<int, DB2Row>>
    {
        private const int HeaderSize = 48;
        private const uint DB2FmtSig = 0x32424457; // WDB2

        private readonly Dictionary<int, DB2Row> m_index = new Dictionary<int, DB2Row>();

        private readonly DB2Row[] m_rows;

        public DB2Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public DB2Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                {
                    throw new InvalidDataException("DB2 file is corrupted!");
                }

                if (reader.ReadUInt32() != DB2FmtSig)
                {
                    throw new InvalidDataException("DB2 file is corrupted!");
                }

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                // WDB2 specific fields
                var tableHash = reader.ReadUInt32(); // new field in WDB2
                var build = reader.ReadUInt32(); // new field in WDB2
                var unk1 = reader.ReadUInt32(); // new field in WDB2

                if (build > 12880) // new extended header
                {
                    var MinId = reader.ReadInt32(); // new field in WDB2
                    var MaxId = reader.ReadInt32(); // new field in WDB2
                    var locale = reader.ReadInt32(); // new field in WDB2
                    var unk5 = reader.ReadInt32(); // new field in WDB2

                    if (MaxId != 0)
                    {
                        var diff = MaxId - MinId + 1; // blizzard is weird people...
                        reader.ReadBytes(diff * 4); // an index for rows
                        reader.ReadBytes(diff * 2); // a memory allocation bank
                    }
                }

                m_rows = new DB2Row[RecordsCount];

                for (var i = 0; i < RecordsCount; i++)
                {
                    m_rows[i] = new DB2Row(this, reader.ReadBytes(RecordSize));

                    var idx = BitConverter.ToInt32(m_rows[i].Data, 0);

                    if (idx < MinIndex)
                    {
                        MinIndex = idx;
                    }

                    if (idx > MaxIndex)
                    {
                        MaxIndex = idx;
                    }

                    m_index[idx] = m_rows[i];
                }

                StringTable = reader.ReadBytes(StringTableSize);
            }
        }

        public int RecordsCount { get; }
        public int FieldsCount { get; }
        public int RecordSize { get; }
        public int StringTableSize { get; }
        public int MinIndex { get; }
        public int MaxIndex { get; }
        public byte[] StringTable { get; }

        public IEnumerator<KeyValuePair<int, DB2Row>> GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        public DB2Row GetRow(int index)
        {
            DB2Row row;
            m_index.TryGetValue(index, out row);
            return row;
        }

        public bool HasRow(int index)
        {
            return m_index.ContainsKey(index);
        }
    }
}
