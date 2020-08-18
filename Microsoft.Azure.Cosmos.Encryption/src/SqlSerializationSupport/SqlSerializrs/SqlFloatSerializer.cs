using System;
using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlFloatSerializer : SqlSerializer<double>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_Float";

        public override double Deserialize(byte[] bytes) => ToDouble(bytes, 0);

        public override byte[] Serialize(double value) 
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            return GetBytes(value);
        }
    }
}
