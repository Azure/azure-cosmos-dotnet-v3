// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <inheritdoc/>
    public class ByteArraySerializer : Serializer<byte[]>
    {
        /// <inheritdoc/>
        public override string Identifier => "ByteArray";

        /// <inheritdoc/>
        public override byte[] Deserialize(byte[] bytes)
        {
            return bytes;
        }

        /// <inheritdoc/>
        public override byte[] Serialize(byte[] value)
        {
            return value;
        }
    }
}
