// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <inheritdoc/>
    public class NullableDoubleSerializer : Serializer<double?>
    {
        private static readonly DoubleSerializer Serializer = new DoubleSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Float64_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override double? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (double?)null : Serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(double? value)
        {
            return value.IsNull() ? null : Serializer.Serialize(value.Value);
        }
    }
}
