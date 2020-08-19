// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <inheritdoc/>
    public class NullableSByteSerializer : Serializer<sbyte?>
    {
        private static readonly SByteSerializer Serializer = new SByteSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SByte_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 1.
        /// </exception>
        public override sbyte? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (sbyte?)null : Serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 1.
        /// </returns>
        public override byte[] Serialize(sbyte? value)
        {
            return value.IsNull() ? null : Serializer.Serialize(value.Value);
        }
    }
}
