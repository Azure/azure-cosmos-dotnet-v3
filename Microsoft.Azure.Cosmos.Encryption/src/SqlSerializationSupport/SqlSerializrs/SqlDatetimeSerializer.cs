using System;
using System.Data.SqlTypes;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlDatetimeSerializer : SqlSerializer<DateTime>
    {
        private static readonly DateTime MinValue = DateTime.Parse("1753-01-01 00:00:00");

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTime";

        public override DateTime Deserialize(byte[] bytes)
        {
            int dayTicks = ToInt32(bytes.Take(4).ToArray(), 0);
            int timeTicks = ToInt32(bytes.Skip(4).Take(4).ToArray(), 0);
            SqlDateTime sqlDateTime = new SqlDateTime(dayTicks, timeTicks);
            return sqlDateTime.Value;
        }

        public override byte[] Serialize(DateTime value)
        {
            if (value < MinValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            SqlDateTime sqlDateTime = new SqlDateTime(value);
            byte[] dayTicks = GetBytes(sqlDateTime.DayTicks);
            byte[] timeTicks = GetBytes(sqlDateTime.TimeTicks);
            return dayTicks.Concat(timeTicks).ToArray();
        }
    }
}
