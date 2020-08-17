namespace Microsoft.Azure.Cosmos.Encryption
{
    using static System.BitConverter;

    public sealed class SqlBitSerializer : SqlSerializer<bool>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_Bit";

        public override bool Deserialize(byte[] bytes) => ToBoolean(bytes, 0);

        public override byte[] Serialize(bool value)
        {
            long longValue = value ? 1 : 0;
            return GetBytes(longValue);
        }
    }
}
