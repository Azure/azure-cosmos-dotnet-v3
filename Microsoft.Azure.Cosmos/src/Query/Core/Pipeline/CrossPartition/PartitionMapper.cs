// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal static class PartitionMapper
    {
        public static TryCatch<PartitionMapping<PartitionedToken>> MonadicGetPartitionMapping<PartitionedToken>(
            IReadOnlyList<FeedRangeEpk> partitionKeyRanges,
            IReadOnlyList<PartitionedToken> partitionedContinuationTokens)
            where PartitionedToken : IPartitionedToken
        {
            if (partitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRanges));
            }

            if (partitionedContinuationTokens == null)
            {
                throw new ArgumentNullException(nameof(partitionedContinuationTokens));
            }

            if (partitionKeyRanges.Count < 1)
            {
                throw new ArgumentException(nameof(partitionKeyRanges));
            }

            if (partitionedContinuationTokens.Count < 1)
            {
                throw new ArgumentException(nameof(partitionKeyRanges));
            }

            if (partitionedContinuationTokens.Count > partitionKeyRanges.Count)
            {
                throw new ArgumentException($"{nameof(partitionedContinuationTokens)} can not have more elements than {nameof(partitionKeyRanges)}.");
            }

            // Find the continuation token for the partition we left off on:
            PartitionedToken firstContinuationToken = partitionedContinuationTokens
                .OrderBy((partitionedToken) => partitionedToken.Range.Min)
                .First();

            // Segment the ranges based off that:
            ReadOnlyMemory<FeedRangeEpk> sortedRanges = partitionKeyRanges
                .OrderBy((partitionKeyRange) => partitionKeyRange.Range.Min)
                .ToArray();

            FeedRangeEpk firstContinuationRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: firstContinuationToken.Range.Min,
                    max: firstContinuationToken.Range.Max,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            int matchedIndex = sortedRanges.Span.BinarySearch(
                firstContinuationRange,
                Comparer<FeedRangeEpk>.Create((range1, range2) => string.CompareOrdinal(range1.Range.Min, range2.Range.Min)));
            if (matchedIndex < 0)
            {
                if (partitionKeyRanges.Count != 1)
                {
                    return TryCatch<PartitionMapping<PartitionedToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"{RMResources.InvalidContinuationToken} - Could not find continuation token: {firstContinuationToken}"));
                }

                // The user is doing a partition key query that got split, so it no longer aligns with our continuation token.
                matchedIndex = 0;
            }

            ReadOnlyMemory<FeedRangeEpk> partitionsLeftOfTarget = matchedIndex == 0 ? ReadOnlyMemory<FeedRangeEpk>.Empty : sortedRanges.Slice(start: 0, length: matchedIndex);
            ReadOnlyMemory<FeedRangeEpk> targetPartition = sortedRanges.Slice(start: matchedIndex, length: 1);
            ReadOnlyMemory<FeedRangeEpk> partitionsRightOfTarget = matchedIndex == sortedRanges.Length - 1 ? ReadOnlyMemory<FeedRangeEpk>.Empty : sortedRanges.Slice(start: matchedIndex + 1);

            // Create the continuation token mapping for each region.
            IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> mappingForPartitionsLeftOfTarget = MatchRangesToContinuationTokens(
                partitionsLeftOfTarget,
                partitionedContinuationTokens);
            IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> mappingForTargetPartition = MatchRangesToContinuationTokens(
                targetPartition,
                partitionedContinuationTokens);
            IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> mappingForPartitionsRightOfTarget = MatchRangesToContinuationTokens(
                partitionsRightOfTarget,
                partitionedContinuationTokens);

            return TryCatch<PartitionMapping<PartitionedToken>>.FromResult(
                new PartitionMapping<PartitionedToken>(
                    partitionsLeftOfTarget: mappingForPartitionsLeftOfTarget,
                    targetPartition: mappingForTargetPartition,
                    partitionsRightOfTarget: mappingForPartitionsRightOfTarget));
        }

        /// <summary>
        /// Matches ranges to their corresponding continuation token.
        /// Note that most ranges don't have a corresponding continuation token, so their value will be set to null.
        /// Also note that in the event of a split two or more ranges will match to the same continuation token.
        /// </summary>
        /// <typeparam name="PartitionedToken">The type of token we are matching with.</typeparam>
        /// <param name="partitionKeyRanges">The partition key ranges to match.</param>
        /// <param name="partitionedContinuationTokens">The continuation tokens to match with.</param>
        /// <returns>A dictionary of ranges matched with their continuation tokens.</returns>
        public static IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> MatchRangesToContinuationTokens<PartitionedToken>(
            ReadOnlyMemory<FeedRangeEpk> partitionKeyRanges,
            IReadOnlyList<PartitionedToken> partitionedContinuationTokens)
            where PartitionedToken : IPartitionedToken
        {
            if (partitionedContinuationTokens == null)
            {
                throw new ArgumentNullException(nameof(partitionedContinuationTokens));
            }

            Dictionary<FeedRangeEpk, PartitionedToken> partitionKeyRangeToToken = new Dictionary<FeedRangeEpk, PartitionedToken>();
            ReadOnlySpan<FeedRangeEpk> partitionKeyRangeSpan = partitionKeyRanges.Span;
            for (int i = 0; i < partitionKeyRangeSpan.Length; i++)
            {
                FeedRangeEpk feedRange = partitionKeyRangeSpan[i];
                foreach (PartitionedToken partitionedToken in partitionedContinuationTokens)
                {
                    bool rightOfStart = (partitionedToken.Range.Min == string.Empty)
                        || ((feedRange.Range.Min != string.Empty) && (feedRange.Range.Min.CompareTo(partitionedToken.Range.Min) >= 0));
                    bool leftOfEnd = (partitionedToken.Range.Max == string.Empty)
                        || ((feedRange.Range.Max != string.Empty) && (feedRange.Range.Max.CompareTo(partitionedToken.Range.Max) <= 0));
                    // See if continuation token includes the range
                    if (rightOfStart && leftOfEnd)
                    {
                        partitionKeyRangeToToken[feedRange] = partitionedToken;
                        break;
                    }
                }

                if (!partitionKeyRangeToToken.ContainsKey(feedRange))
                {
                    // Could not find a matching token so just set it to null
                    partitionKeyRangeToToken[feedRange] = default;
                }
            }

            return partitionKeyRangeToToken;
        }

        public readonly struct PartitionMapping<T>
        {
            public PartitionMapping(
                IReadOnlyDictionary<FeedRangeEpk, T> partitionsLeftOfTarget,
                IReadOnlyDictionary<FeedRangeEpk, T> targetPartition,
                IReadOnlyDictionary<FeedRangeEpk, T> partitionsRightOfTarget)
            {
                this.PartitionsLeftOfTarget = partitionsLeftOfTarget ?? throw new ArgumentNullException(nameof(partitionsLeftOfTarget));
                this.TargetPartition = targetPartition ?? throw new ArgumentNullException(nameof(targetPartition));
                this.PartitionsRightOfTarget = partitionsRightOfTarget ?? throw new ArgumentNullException(nameof(partitionsRightOfTarget));
            }

            public IReadOnlyDictionary<FeedRangeEpk, T> PartitionsLeftOfTarget { get; }
            public IReadOnlyDictionary<FeedRangeEpk, T> TargetPartition { get; }
            public IReadOnlyDictionary<FeedRangeEpk, T> PartitionsRightOfTarget { get; }
        }
    }
}
