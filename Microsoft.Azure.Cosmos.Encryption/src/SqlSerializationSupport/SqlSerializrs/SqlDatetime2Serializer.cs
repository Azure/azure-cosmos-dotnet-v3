namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.BitConverter;
    using static Microsoft.Azure.Cosmos.Encryption.SqlTimeSerializer;

    public sealed class SqlDatetime2Serializer : SqlSerializer<DateTime>
    {
        private const int MaxPrecision = 7;
        private const int MinPrecision = 0;
        private const int DefaultPrecision = 7;
        private readonly SqlDateSerializer sqlDateSerializer = new SqlDateSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Datetime2";

        private int precision;

        public int Precision
        {
            get { return this.precision; }

            set
            {
                if (value < MinPrecision || value > MaxPrecision)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }
                this.precision = value;
            }
        }

        public SqlDatetime2Serializer(int precision = DefaultPrecision)
        {
            this.Precision = precision;
        }

        public override DateTime Deserialize(byte[] bytes)
        {
            byte[] padding = { 0, 0, 0 };
            byte[] timePart = bytes.Take(5).Concat(padding).ToArray();
            byte[] datePart = bytes.Skip(5).ToArray();
            long timeTicks = ToInt64(timePart, 0);
            DateTime dateTime = this.sqlDateSerializer.Deserialize(datePart);
            return dateTime.AddTicks(timeTicks);
        }

        public override byte[] Serialize(DateTime value)
        {
            DateTime normalizedValue = NormalizeToScale(value, this.Precision);
            long time = normalizedValue.TimeOfDay.Ticks;
            byte[] timePart = GetBytes(time).Take(5).ToArray();
            byte[] datePart = this.sqlDateSerializer.Serialize(value);
            return timePart.Concat(datePart).ToArray();
        }

        public static DateTime NormalizeToScale(DateTime dateTime, int scale)
        {
            long normalizedTicksOffset = (dateTime.TimeOfDay.Ticks / precisionScale[scale] * precisionScale[scale]) - dateTime.TimeOfDay.Ticks;
            return dateTime.AddTicks(normalizedTicksOffset);
        }
    }
}
