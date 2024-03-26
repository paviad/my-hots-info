using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    internal class DBCRow
    {
        private readonly DBCReader m_reader;

        public DBCRow(DBCReader reader, byte[] data)
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

    internal class DBCReader : IEnumerable<KeyValuePair<int, DBCRow>>
    {
        private const uint HeaderSize = 20;
        private const uint DBCFmtSig = 0x43424457; // WDBC

        private readonly DBCRow[] m_rows;

        private readonly Dictionary<int, DBCRow> m_index = new Dictionary<int, DBCRow>();

        public DBCReader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public DBCReader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                {
                    throw new InvalidDataException("File DBC is corrupted!");
                }

                if (reader.ReadUInt32() != DBCFmtSig)
                {
                    throw new InvalidDataException("File DBC is corrupted!");
                }

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                m_rows = new DBCRow[RecordsCount];

                for (var i = 0; i < RecordsCount; i++)
                {
                    m_rows[i] = new DBCRow(this, reader.ReadBytes(RecordSize));

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
        public int MinIndex { get; } = int.MaxValue;
        public int MaxIndex { get; } = int.MinValue;

        public byte[] StringTable { get; }

        public IEnumerator<KeyValuePair<int, DBCRow>> GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        public DBCRow GetRow(int index)
        {
            if (!m_index.ContainsKey(index))
            {
                return null;
            }

            return m_index[index];
        }

        public bool HasRow(int index)
        {
            return m_index.ContainsKey(index);
        }
    }
}
