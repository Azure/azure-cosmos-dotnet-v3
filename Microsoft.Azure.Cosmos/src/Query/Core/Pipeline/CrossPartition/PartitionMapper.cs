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
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
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
            ReadOnlyMemory<PartitionKeyRange> sortedRanges = partitionKeyRanges
                .OrderBy((partitionKeyRange) => partitionKeyRange.MinInclusive)
                .ToArray();

            PartitionKeyRange firstContinuationRange = new PartitionKeyRange
            {
                MinInclusive = firstContinuationToken.Range.Min,
                MaxExclusive = firstContinuationToken.Range.Max
            };

            int matchedIndex = sortedRanges.Span.BinarySearch(
                firstContinuationRange,
                Comparer<PartitionKeyRange>.Create((range1, range2) => string.CompareOrdinal(range1.MinInclusive, range2.MinInclusive)));
            if (matchedIndex < 0)
            {
                return TryCatch<PartitionMapping<PartitionedToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"{RMResources.InvalidContinuationToken} - Could not find continuation token: {firstContinuationToken}"));
            }

            ReadOnlyMemory<PartitionKeyRange> partitionsLeftOfTarget = matchedIndex == 0 ? ReadOnlyMemory<PartitionKeyRange>.Empty : sortedRanges.Slice(start: 0, length: matchedIndex);
            ReadOnlyMemory<PartitionKeyRange> targetPartition = sortedRanges.Slice(start: matchedIndex, length: 1);
            ReadOnlyMemory<PartitionKeyRange> partitionsRightOfTarget = matchedIndex == sortedRanges.Length - 1 ? ReadOnlyMemory<PartitionKeyRange>.Empty : sortedRanges.Slice(start: matchedIndex + 1);

            // Create the continuation token mapping for each region.
            IReadOnlyDictionary<PartitionKeyRange, PartitionedToken> mappingForPartitionsLeftOfTarget = MatchRangesToContinuationTokens(
                partitionsLeftOfTarget,
                partitionedContinuationTokens);
            IReadOnlyDictionary<PartitionKeyRange, PartitionedToken> mappingForTargetPartition = MatchRangesToContinuationTokens(
                targetPartition,
                partitionedContinuationTokens);
            IReadOnlyDictionary<PartitionKeyRange, PartitionedToken> mappingForPartitionsRightOfTarget = MatchRangesToContinuationTokens(
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
        public static IReadOnlyDictionary<PartitionKeyRange, PartitionedToken> MatchRangesToContinuationTokens<PartitionedToken>(
            ReadOnlyMemory<PartitionKeyRange> partitionKeyRanges,
            IReadOnlyList<PartitionedToken> partitionedContinuationTokens)
            where PartitionedToken : IPartitionedToken
        {
            if (partitionedContinuationTokens == null)
            {
                throw new ArgumentNullException(nameof(partitionedContinuationTokens));
            }

            Dictionary<PartitionKeyRange, PartitionedToken> partitionKeyRangeToToken = new Dictionary<PartitionKeyRange, PartitionedToken>();
            ReadOnlySpan<PartitionKeyRange> partitionKeyRangeSpan = partitionKeyRanges.Span;
            for (int i = 0; i < partitionKeyRangeSpan.Length; i++)
            {
                PartitionKeyRange partitionKeyRange = partitionKeyRangeSpan[i];
                foreach (PartitionedToken partitionedToken in partitionedContinuationTokens)
                {
                    bool rightOfStart = (partitionedToken.Range.Min == string.Empty)
                        || ((partitionKeyRange.MinInclusive != string.Empty) && (partitionKeyRange.MinInclusive.CompareTo(partitionedToken.Range.Min) >= 0));
                    bool leftOfEnd = (partitionedToken.Range.Max == string.Empty)
                        || ((partitionKeyRange.MaxExclusive != string.Empty) && (partitionKeyRange.MaxExclusive.CompareTo(partitionedToken.Range.Max) <= 0));
                    // See if continuation token includes the range
                    if (rightOfStart && leftOfEnd)
                    {
                        partitionKeyRangeToToken[partitionKeyRange] = partitionedToken;
                        break;
                    }
                }

                if (!partitionKeyRangeToToken.ContainsKey(partitionKeyRange))
                {
                    // Could not find a matching token so just set it to null
                    partitionKeyRangeToToken[partitionKeyRange] = default;
                }
            }

            return partitionKeyRangeToToken;
        }

        public readonly struct PartitionMapping<T>
        {
            public PartitionMapping(
                IReadOnlyDictionary<PartitionKeyRange, T> partitionsLeftOfTarget,
                IReadOnlyDictionary<PartitionKeyRange, T> targetPartition,
                IReadOnlyDictionary<PartitionKeyRange, T> partitionsRightOfTarget)
            {
                this.PartitionsLeftOfTarget = partitionsLeftOfTarget ?? throw new ArgumentNullException(nameof(partitionsLeftOfTarget));
                this.TargetPartition = targetPartition ?? throw new ArgumentNullException(nameof(targetPartition));
                this.PartitionsRightOfTarget = partitionsRightOfTarget ?? throw new ArgumentNullException(nameof(partitionsRightOfTarget));
            }

            public IReadOnlyDictionary<PartitionKeyRange, T> PartitionsLeftOfTarget { get; }
            public IReadOnlyDictionary<PartitionKeyRange, T> TargetPartition { get; }
            public IReadOnlyDictionary<PartitionKeyRange, T> PartitionsRightOfTarget { get; }
        }
    }
}
