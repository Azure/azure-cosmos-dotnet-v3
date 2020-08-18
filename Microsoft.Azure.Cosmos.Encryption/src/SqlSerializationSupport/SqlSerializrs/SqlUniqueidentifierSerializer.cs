namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    public sealed class SqlUniqueidentifierSerializer : SqlSerializer<Guid>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_UniqueIdentifier";

        public override Guid Deserialize(byte[] bytes) => new Guid(bytes);

        public override byte[] Serialize(Guid value) => value.ToByteArray();
    }
}
