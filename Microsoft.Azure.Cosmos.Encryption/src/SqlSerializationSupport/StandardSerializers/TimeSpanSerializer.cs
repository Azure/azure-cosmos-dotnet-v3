// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using static System.BitConverter;

    /// <inheritdoc/>
    public class TimeSpanSerializer : Serializer<TimeSpan>
    {
        /// <inheritdoc/>
        public override string Identifier => "Time";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override TimeSpan Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(long), nameof(bytes));

            return new TimeSpan(ToInt64(bytes, 0));
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(TimeSpan value) => GetBytes(value.Ticks);
    }
}
