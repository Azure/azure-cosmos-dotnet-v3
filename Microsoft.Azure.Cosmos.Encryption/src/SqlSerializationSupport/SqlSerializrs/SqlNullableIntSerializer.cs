namespace Microsoft.Azure.Cosmos.Encryption
{
    public class SqlNullableIntSerializer : SqlSerializer<int?>
    {
        /// <inheritdoc/>
        public override string Identifier =>"SQL_Int_Nullable";

        private static readonly SqlIntSerializer serializer = new SqlIntSerializer();

        public override int? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (int?)null : serializer.Deserialize(bytes);
        }

        public override byte[] Serialize(int? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
