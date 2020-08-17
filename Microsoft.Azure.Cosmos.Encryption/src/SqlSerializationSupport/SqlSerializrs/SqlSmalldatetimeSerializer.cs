using System;
using System.Data.SqlTypes;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlSmalldatetimeSerializer : SqlSerializer<DateTime>
    {
        private static readonly DateTime MinValue = DateTime.Parse("1900-01-01 00:00:00");
        private static readonly DateTime MaxValue = DateTime.Parse("2079-06-06 23:59:59");

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallDateTime";

        public override DateTime Deserialize(byte[] bytes)
        {
            int dayTicks = ToUInt16(bytes.Take(2).ToArray(), 0);
            int timeTicks = ToUInt16(bytes.Skip(2).Take(2).ToArray(), 0) * SqlDateTime.SQLTicksPerMinute;
            SqlDateTime sqlDateTime = new SqlDateTime(dayTicks, timeTicks);
            return sqlDateTime.Value;
        }

        public override byte[] Serialize(DateTime value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            SqlDateTime sqlDateTime = new SqlDateTime(value.AddSeconds(30));
            byte[] dayTicks = GetBytes((short)sqlDateTime.DayTicks);
            byte[] timeTicks = GetBytes((short)(sqlDateTime.TimeTicks / SqlDateTime.SQLTicksPerMinute));
            return dayTicks.Concat(timeTicks).ToArray();
        }
    }
}
