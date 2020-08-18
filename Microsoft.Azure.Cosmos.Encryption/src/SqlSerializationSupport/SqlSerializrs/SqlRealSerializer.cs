using System;
using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlRealSerializer : SqlSerializer<float>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_Real";

        public override float Deserialize(byte[] bytes) => ToSingle(bytes, 0);

        public override byte[] Serialize(float value)
        {
            if (float.IsInfinity(value) || float.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            return GetBytes(value);
        }
    }
}
