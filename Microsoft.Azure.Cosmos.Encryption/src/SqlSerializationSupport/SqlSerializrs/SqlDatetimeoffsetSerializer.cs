using System;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlDatetimeoffsetSerializer : SqlSerializer<DateTimeOffset>
    {
        private const int MaxScale = 7;
        private const int MinScale = 0;
        private const int DefaultScale = 7;
        private readonly SqlDatetime2Serializer sqlDatetime2Serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTimeOffset";

        private int scale;

        public int Scale
        {
            get => this.scale;
            set
            {
                if (value < MinScale || value > MaxScale)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }
                this.scale = value;
            }
        }

        public SqlDatetimeoffsetSerializer(int scale = DefaultScale)
        {
            this.Scale = scale;
            this.sqlDatetime2Serializer = new SqlDatetime2Serializer(scale);
        }

        public override DateTimeOffset Deserialize(byte[] bytes)
        {
            byte[] dateTimePart = bytes.Take(8).ToArray();
            byte[] offsetPart = bytes.Skip(8).ToArray();
            short minutes = ToInt16(offsetPart, 0);
            DateTime dateTime = this.sqlDatetime2Serializer.Deserialize(dateTimePart).AddMinutes(minutes);
            return new DateTimeOffset(dateTime);
        }

        public override byte[] Serialize(DateTimeOffset value)
        {
            byte[] datetimePart = this.sqlDatetime2Serializer.Serialize(value.UtcDateTime);
            short offsetMinutes = (short)value.Offset.TotalMinutes;
            byte[] offsetPart = GetBytes(offsetMinutes);
            return datetimePart.Concat(offsetPart).ToArray();
        }
    }
}
