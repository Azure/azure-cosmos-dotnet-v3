using System;
using System.Data.SqlTypes;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlDecimalSerializer : SqlSerializer<decimal>
    {
        private const int SignIndex = 3;
        private const int MaxPrecision = 38;
        private const int MinPrecision = 1;
        private const int DefaultPrecision = 18;
        private const int MinScale = 0;
        private const int DefaultScale = 0;
        private static readonly byte[] positiveSign = { 1 };
        private static readonly byte[] negativeSign = { 0 };
        private static readonly byte[] padding = { 0, 0, 0, 0 };

        /// <inheritdoc/>
        public override string Identifier => "SQL_Decimal";

        private int precision;

        public int Precision
        {
            get => this.precision;
            set
            {
                if (value < MinPrecision || value > MaxPrecision)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }

                this.precision = value;
            }
        }

        private int scale;

        public int Scale
        {
            get => this.scale;
            set
            {
                if (value < MinScale || value > this.Precision)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }

                this.scale = value;
            }
        }

        public SqlDecimalSerializer(int precision = DefaultPrecision, int scale = DefaultScale)
        {
            this.Precision = precision;
            this.Scale = scale;
        }

        public override decimal Deserialize(byte[] bytes)
        {
            int low = ToInt32(bytes.Skip(1).Take(4).ToArray(), 0);
            int middle = ToInt32(bytes.Skip(5).Take(4).ToArray(), 0);
            int high = ToInt32(bytes.Skip(9).Take(4).ToArray(), 0);
            bool isNegative = bytes[0] == negativeSign[0];

            return new decimal(low, middle, high, isNegative, (byte)this.Scale);
        }

        public override byte[] Serialize(decimal value)
        {
            SqlDecimal sqlDecimal = new SqlDecimal(value);

            if (sqlDecimal.Precision > this.Precision)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            int[] decimalBits = decimal.GetBits(value);
            byte[] sign = this.IsPositive(decimalBits[SignIndex]) ? positiveSign : negativeSign;
            byte[] integerPart = decimalBits.Take(3).SelectMany(GetBytes).ToArray();
            return sign.Concat(integerPart).Concat(padding).ToArray();
        }

        private bool IsPositive(int i) => i > -1;
    }
}
