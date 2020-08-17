using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlIntSerializer : SqlSerializer<int>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_Int";

        public override int Deserialize(byte[] bytes) => ToInt32(bytes, 0);

        public override byte[] Serialize(int value) => GetBytes((long)value);
    }
}
