using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlSmallintSerializer : SqlSerializer<short>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallInt";

        public override short Deserialize(byte[] bytes) => ToInt16(bytes, 0);

        public override byte[] Serialize(short value) => GetBytes((long)value);
    }
}
