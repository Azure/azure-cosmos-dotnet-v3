// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using static System.BitConverter;

    /// <inheritdoc/>
    public class FloatSerializer : Serializer<float>
    {
        /// <inheritdoc/>
        public override string Identifier => "Float32";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override float Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(float), nameof(bytes));

            return ToSingle(bytes, 0);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        public override byte[] Serialize(float value) => GetBytes(value);
    }
}
