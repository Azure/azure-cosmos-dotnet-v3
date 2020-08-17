namespace Microsoft.Azure.Cosmos.Encryption
{
    using static System.BitConverter;

    /// <inheritdoc />
    public sealed class SqlBigIntSerializer : SqlSerializer<long>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_BigInt";

        /// <inheritdoc/>
        public override long Deserialize(byte[] bytes) => ToInt64(bytes, 0);

        /// <inheritdoc/>
        public override byte[] Serialize(long value) => GetBytes(value);
    }
}
