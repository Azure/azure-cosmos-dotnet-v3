using System;

using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlSmallmoneySerializer : SqlSerializer<decimal>
    {        
        private const decimal MinValue = -214748.3648M;
        private const decimal MaxValue = 214748.3647M;
        private static readonly SqlMoneySerializer sqlMoneySerializer = new SqlMoneySerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallMoney";

        public override decimal Deserialize(byte[] bytes)
        {
            return sqlMoneySerializer.Deserialize(bytes);
        }

        public override byte[] Serialize(decimal value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            return sqlMoneySerializer.Serialize(value);
        }
    }
}
