using System;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public class SqlNullableDatetimeoffsetSerializer : SqlSerializer<DateTimeOffset?>
    {
        private const int DefaultScale = 7;
        private readonly SqlDatetimeoffsetSerializer serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTimeOffset_Nullable";

        public SqlNullableDatetimeoffsetSerializer(int scale = DefaultScale)
        {
            this.serializer = new SqlDatetimeoffsetSerializer(scale);
        }

        public override DateTimeOffset? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTimeOffset?)null : this.serializer.Deserialize(bytes);
        }

        public override byte[] Serialize(DateTimeOffset? value)
        {
            return value.IsNull() ? null : this.serializer.Serialize(value.Value);
        }
    }
}
