// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <inheritdoc/>
    public class NullableByteSerializer : Serializer<byte?>
    {
        private static readonly ByteSerializer serializer = new ByteSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Byte_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 1.
        /// </exception>
        public override byte? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (byte?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 1.
        /// </returns>
        public override byte[] Serialize(byte? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
