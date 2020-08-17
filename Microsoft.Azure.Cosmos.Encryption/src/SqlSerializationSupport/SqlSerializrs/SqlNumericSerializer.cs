namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlNumericSerializer : SqlSerializer<decimal>
    {
        private const int DefaultPrecision = 18;
        private const int DefaultScale = 0;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Numeric";

        private SqlDecimalSerializer SqlDecimalSerializer { get; set; }

        public SqlNumericSerializer(int precision = DefaultPrecision, int scale = DefaultScale)
        {
            this.SqlDecimalSerializer = new SqlDecimalSerializer(precision, scale);
        }

        public override decimal Deserialize(byte[] bytes) => this.SqlDecimalSerializer.Deserialize(bytes);

        public override byte[] Serialize(decimal value) => this.SqlDecimalSerializer.Serialize(value);
    }
}
