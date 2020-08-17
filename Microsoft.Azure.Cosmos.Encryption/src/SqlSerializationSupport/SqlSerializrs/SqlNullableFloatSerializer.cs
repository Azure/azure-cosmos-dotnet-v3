namespace Microsoft.Azure.Cosmos.Encryption
{
    public class SqlNullableFloatSerializer : SqlSerializer<double?>
    {
        private static readonly SqlFloatSerializer serializer = new SqlFloatSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Float_Nullable";

        public override double? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (double?)null : serializer.Deserialize(bytes);
        }

        public override byte[] Serialize(double? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
