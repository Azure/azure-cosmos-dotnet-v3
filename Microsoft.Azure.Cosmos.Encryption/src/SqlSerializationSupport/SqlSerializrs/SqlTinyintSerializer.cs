using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlTinyintSerializer : SqlSerializer<byte>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_TinyInt";

        public override byte Deserialize(byte[] bytes) => bytes[0];

        public override byte[] Serialize(byte value) => GetBytes((long)value);
    }
}
