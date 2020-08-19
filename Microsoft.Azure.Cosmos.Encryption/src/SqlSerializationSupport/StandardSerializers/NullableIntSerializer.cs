// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <inheritdoc/>
    public class NullableIntSerializer : Serializer<int?>
    {
        private static readonly IntSerializer Serializer = new IntSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Int32_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override int? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (int?)null : Serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        public override byte[] Serialize(int? value)
        {
            return value.IsNull() ? null : Serializer.Serialize(value.Value);
        }
    }
}
