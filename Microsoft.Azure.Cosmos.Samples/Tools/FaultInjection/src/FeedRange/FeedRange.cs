﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Represents a unit of feed consumption that can be used as unit of parallelism.
    /// </summary>
    [Serializable]
    public abstract class FeedRange
    {
        /// <summary>
        /// Gets a string representation of the current range.
        /// </summary>
        /// <returns>A string representation of the current token.</returns>
        public abstract string ToJsonString();

        /// <summary>
        /// Creates a range from a previously obtained string representation.
        /// </summary>
        /// <param name="toStringValue">A string representation obtained from <see cref="ToJsonString" />.</param>
        /// <returns>A <see cref="FeedRange" />.</returns>
        /// <exception cref="ArgumentException">If the <paramref name="toStringValue"/> does not represent a valid value.</exception>
        public static FeedRange FromJsonString(string toStringValue)
        {
            if (!FeedRangeInternal.TryParse(toStringValue, out FeedRangeInternal parsedRange))
            {
                throw new ArgumentException(string.Format(ClientResources.FeedToken_UnknownFormat, toStringValue));
            }

            return parsedRange;
        }

        /// <summary>
        /// Creates a feed range that span only a single <see cref="PartitionKey"/> value.
        /// </summary>
        /// <param name="partitionKey">The partition key value to create a feed range from.</param>
        /// <returns>The feed range that spans the partition.</returns>
        public static FeedRange FromPartitionKey(PartitionKey partitionKey)
        {
            return new FeedRangePartitionKey(partitionKey);
        }
    }
}
