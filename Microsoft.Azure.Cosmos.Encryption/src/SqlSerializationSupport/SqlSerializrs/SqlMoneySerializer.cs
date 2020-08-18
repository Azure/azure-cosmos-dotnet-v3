using System;
using System.Data.SqlTypes;
using System.Linq;

using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlMoneySerializer : SqlSerializer<decimal>
    {
        private const byte Scale = 4;
        private const decimal MinValue = -922337203685477.5808M;
        private const decimal MaxValue = 922337203685477.5807M;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Money";

        public override decimal Deserialize(byte[] bytes)
        {
            uint low = ToUInt32(bytes, 4);
            int middle = ToInt32(bytes, 0);
            long longValue = ((long)middle << 32) + low;
            bool isNegative = longValue < 0;
            int sign = isNegative ? -1 : 1;
            long signedLongValue = longValue * sign;
            int signedLow = (int)(signedLongValue & uint.MaxValue);
            int signedMiddle = (int)(signedLongValue >> 32);

            return new decimal(signedLow, signedMiddle, 0, isNegative, Scale);
        }

        public override byte[] Serialize(decimal value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            value = this.Normalize(value);
            int sign = value > -1 ? 1 : -1;
            int[] decimalBits = decimal.GetBits(value);
            long longValue = ((long)decimalBits[1] << 32) + (uint)decimalBits[0];
            long signedLongValue = longValue * sign;
            int low = (int)(signedLongValue >> 32);
            int mid = (int)signedLongValue;
            byte[] lowbytes = GetBytes(low);
            byte[] midBytes = GetBytes(mid);

            return lowbytes.Concat(midBytes).ToArray();
        }

        public decimal Normalize(decimal value) => new SqlMoney(value).Value;
    }
}
