// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <inheritdoc/>
    public class NullableTimeSpanSerializer : Serializer<TimeSpan?>
    {
        private static readonly TimeSpanSerializer Serializer = new TimeSpanSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Time_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override TimeSpan? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (TimeSpan?)null : Serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(TimeSpan? value)
        {
            return value.IsNull() ? null : Serializer.Serialize(value.Value);
        }
    }
}
