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

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif 
        static class PartitionMapper
    {
        public static TryCatch<PartitionMapping<PartitionedToken>> MonadicGetPartitionMapping<PartitionedToken>(
            IReadOnlyList<FeedRangeEpkRange> feedRanges,
            IReadOnlyList<PartitionedToken> tokens)
            where PartitionedToken : IPartitionedToken
        {
            if (feedRanges == null)
            {
                throw new ArgumentNullException(nameof(feedRanges));
            }

            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            if (feedRanges.Count < 1)
            {
                throw new ArgumentException(nameof(feedRanges));
            }

            if (tokens.Count < 1)
            {
                throw new ArgumentException(nameof(feedRanges));
            }

            List<FeedRangeEpkRange> mergedFeedRanges = MergeRangesWherePossible(feedRanges);
            List<(FeedRangeEpkRange, PartitionedToken)> splitRangesAndTokens = SplitRangesBasedOffContinuationToken(mergedFeedRanges, tokens);
            FeedRangeEpkRange targetFeedRange = GetTargetFeedRange(tokens);
            return MonadicConstructPartitionMapping(
                splitRangesAndTokens,
                tokens,
                targetFeedRange);
        }

        /// <summary>
        /// Merges all the feed ranges as much as possible.
        /// </summary>
        /// <param name="feedRanges">The ranges to merge.</param>
        /// <returns>The merged ranges</returns>
        /// <example>
        /// [(A, B), (B, C), (E, F), (H, I), (I, J)] 
        ///     => [(A, C), (E, F), (H, J)]
        /// </example>
        private static List<FeedRangeEpkRange> MergeRangesWherePossible(IReadOnlyList<FeedRangeEpkRange> feedRanges)
        {
            Stack<(string min, string max)> mergedRanges = new Stack<(string min, string max)>(feedRanges.Count);
            foreach (FeedRangeEpkRange feedRange in feedRanges.OrderBy(feedRange => feedRange.StartEpkInclusive))
            {
                if (mergedRanges.Count == 0)
                {
                    // If the stack is empty, then just add the range to get things started.
                    mergedRanges.Push((feedRange.StartEpkInclusive, feedRange.EndEpkExclusive));
                }
                else
                {
                    (string min, string max) = mergedRanges.Pop();
                    if (max == feedRange.StartEpkInclusive)
                    {
                        // This means that the ranges are consequtive and can be merged.
                        mergedRanges.Push((min, feedRange.EndEpkExclusive));
                    }
                    else
                    {
                        // Just push the ranges on seperately 
                        mergedRanges.Push((min, max));
                        mergedRanges.Push((feedRange.StartEpkInclusive, feedRange.EndEpkExclusive));
                    }
                }
            }

            List<FeedRangeEpkRange> mergedFeedRanges = mergedRanges
                .Select(range => new FeedRangeEpkRange(range.min, range.max))
                .ToList();

            return mergedFeedRanges;
        }

        /// <summary>
        /// Splits the ranges into the ranges from the continuation token.
        /// </summary>
        /// <typeparam name="PartitionedToken">The partitioned token type.</typeparam>
        /// <param name="feedRanges">The ranges to split.</param>
        /// <param name="tokens">The tokens to split with.</param>
        /// <returns>A list of Range and corresponding token tuple.</returns>
        /// <example>
        /// ranges: [(A, E), (H, K)], 
        /// tokens: [(A, C):5, (I, J): 6] 
        ///     => [(A,C): 5, (C, E): null, (H, I): null, (I, J): 6, (J, K): null]
        /// </example>
        private static List<(FeedRangeEpkRange, PartitionedToken)> SplitRangesBasedOffContinuationToken<PartitionedToken>(
            IReadOnlyList<FeedRangeEpkRange> feedRanges,
            IReadOnlyList<PartitionedToken> tokens)
            where PartitionedToken : IPartitionedToken
        {
            HashSet<FeedRangeEpkRange> remainingRanges = new HashSet<FeedRangeEpkRange>(feedRanges);
            List<(FeedRangeEpkRange, PartitionedToken)> splitRangesAndTokens = new List<(FeedRangeEpkRange, PartitionedToken)>();
            foreach (PartitionedToken partitionedToken in tokens)
            {
                List<FeedRangeEpkRange> rangesThatOverlapToken = remainingRanges
                    .Where(feedRange =>
                    {
                        bool tokenRightOfStart = (feedRange.StartEpkInclusive == string.Empty)
                            || ((partitionedToken.Range.Min != string.Empty) && (partitionedToken.Range.Min.CompareTo(feedRange.StartEpkInclusive) >= 0));
                        bool tokenLeftOfEnd = (feedRange.EndEpkExclusive == string.Empty)
                            || ((partitionedToken.Range.Max != string.Empty) && (partitionedToken.Range.Max.CompareTo(feedRange.EndEpkExclusive) <= 0));
                        
                        bool rangeCompletelyOverlapsToken = tokenRightOfStart && tokenLeftOfEnd;

                        return rangeCompletelyOverlapsToken;
                    })
                    .ToList();

                if (rangesThatOverlapToken.Count == 0)
                {
                    // Do nothing
                }
                else if (rangesThatOverlapToken.Count == 1)
                {
                    FeedRangeEpkRange feedRange = rangesThatOverlapToken.First();
                    // Remove the range and split it into 3 sections:
                    remainingRanges.Remove(feedRange);

                    // 1) Left of Token Range
                    if (feedRange.StartEpkInclusive != partitionedToken.Range.Min)
                    {
                        FeedRangeEpkRange leftOfOverlap = new FeedRangeEpkRange(
                            startEpkInclusive: feedRange.StartEpkInclusive,
                            endEpkExclusive: partitionedToken.Range.Min);
                        remainingRanges.Add(leftOfOverlap);
                    }

                    // 2) Token Range
                    FeedRangeEpkRange overlappingSection = new FeedRangeEpkRange(
                        startEpkInclusive: partitionedToken.Range.Min,
                        endEpkExclusive: partitionedToken.Range.Max);
                    splitRangesAndTokens.Add((overlappingSection, partitionedToken));

                    // 3) Right of Token Range
                    if (partitionedToken.Range.Max != feedRange.EndEpkExclusive)
                    {
                        FeedRangeEpkRange rightOfOverlap = new FeedRangeEpkRange(
                            startEpkInclusive: partitionedToken.Range.Max,
                            endEpkExclusive: feedRange.EndEpkExclusive);
                        remainingRanges.Add(rightOfOverlap);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Token was overlapped by multiple ranges.");
                }
            }

            foreach (FeedRangeEpkRange remainingRange in remainingRanges)
            {
                // Unmatched ranges just match to null tokens
                splitRangesAndTokens.Add((remainingRange, default));
            }

            return splitRangesAndTokens;
        }

        private static FeedRangeEpkRange GetTargetFeedRange<PartitionedToken>(IReadOnlyList<PartitionedToken> tokens)
            where PartitionedToken : IPartitionedToken
        {
            PartitionedToken firstContinuationToken = tokens
                .OrderBy((partitionedToken) => partitionedToken.Range.Min)
                .First();

            FeedRangeEpkRange targetFeedRange = new FeedRangeEpkRange(
                startEpkInclusive: firstContinuationToken.Range.Min,
                endEpkExclusive: firstContinuationToken.Range.Max);

            return targetFeedRange;
        }

        /// <summary>
        /// Segments the ranges and their tokens into a partition mapping.
        /// </summary>
        private static TryCatch<PartitionMapping<PartitionedToken>> MonadicConstructPartitionMapping<PartitionedToken>(
            IReadOnlyList<(FeedRangeEpkRange, PartitionedToken)> splitRangesAndTokens,
            IReadOnlyList<PartitionedToken> tokens,
            FeedRangeEpkRange targetRange)
            where PartitionedToken : IPartitionedToken
        {
            ReadOnlyMemory<(FeedRangeEpkRange range, PartitionedToken token)> sortedRanges = splitRangesAndTokens
                .OrderBy((rangeAndToken) => rangeAndToken.Item1.StartEpkInclusive)
                .ToArray();

            int? matchedIndex = null;
            for (int i = 0; (i < sortedRanges.Length) && !matchedIndex.HasValue; i++)
            {
                (FeedRangeEpkRange range, PartitionedToken token) = sortedRanges.Span[i];
                if (range.Equals(targetRange))
                {
                    matchedIndex = i;
                }
            }

            if (!matchedIndex.HasValue)
            {
                if (splitRangesAndTokens.Count != 1)
                {
                    return TryCatch<PartitionMapping<PartitionedToken>>.FromException(
                        new MalformedContinuationTokenException(
                            $"{RMResources.InvalidContinuationToken} - Could not find continuation token for range: '{targetRange}'"));
                }

                // The user is doing a partition key query that got split, so it no longer aligns with our continuation token.
                sortedRanges = new (FeedRangeEpkRange, PartitionedToken)[] { (sortedRanges.Span[0].range, tokens[0]) };
                matchedIndex = 0;
            }

            ReadOnlyMemory<(FeedRangeEpkRange, PartitionedToken)> partitionsLeftOfTarget;
            if (matchedIndex.Value == 0)
            {
                partitionsLeftOfTarget = ReadOnlyMemory<(FeedRangeEpkRange, PartitionedToken)>.Empty;
            }
            else
            {
                partitionsLeftOfTarget = sortedRanges.Slice(start: 0, length: matchedIndex.Value);
            }

            ReadOnlyMemory<(FeedRangeEpkRange, PartitionedToken)> targetPartition = sortedRanges.Slice(start: matchedIndex.Value, length: 1);

            ReadOnlyMemory<(FeedRangeEpkRange, PartitionedToken)> partitionsRightOfTarget;
            if (matchedIndex.Value == sortedRanges.Length - 1)
            {
                partitionsRightOfTarget = ReadOnlyMemory<(FeedRangeEpkRange, PartitionedToken)>.Empty;
            }
            else
            {
                partitionsRightOfTarget = sortedRanges.Slice(start: matchedIndex.Value + 1);
            }

            static Dictionary<FeedRangeEpkRange, PartitionedToken> CreateMappingFromTuples(ReadOnlySpan<(FeedRangeEpkRange, PartitionedToken)> rangeAndTokens)
            {
                Dictionary<FeedRangeEpkRange, PartitionedToken> mappingForPartitions = new Dictionary<FeedRangeEpkRange, PartitionedToken>();
                foreach ((FeedRangeEpkRange range, PartitionedToken token) in rangeAndTokens)
                {
                    mappingForPartitions[range] = token;
                }

                return mappingForPartitions;
            }

            // Create the continuation token mapping for each region.
            IReadOnlyDictionary<FeedRangeEpkRange, PartitionedToken> mappingForPartitionsLeftOfTarget = CreateMappingFromTuples(partitionsLeftOfTarget.Span);
            IReadOnlyDictionary<FeedRangeEpkRange, PartitionedToken> mappingForTargetPartition = CreateMappingFromTuples(targetPartition.Span);
            IReadOnlyDictionary<FeedRangeEpkRange, PartitionedToken> mappingForPartitionsRightOfTarget = CreateMappingFromTuples(partitionsRightOfTarget.Span);

            return TryCatch<PartitionMapping<PartitionedToken>>.FromResult(
                new PartitionMapping<PartitionedToken>(
                    mappingLeftOfTarget: mappingForPartitionsLeftOfTarget,
                    targetMapping: mappingForTargetPartition,
                    mappingRightOfTarget: mappingForPartitionsRightOfTarget));
        }

        public readonly struct PartitionMapping<T>
        {
            public PartitionMapping(
                IReadOnlyDictionary<FeedRangeEpkRange, T> mappingLeftOfTarget,
                IReadOnlyDictionary<FeedRangeEpkRange, T> targetMapping,
                IReadOnlyDictionary<FeedRangeEpkRange, T> mappingRightOfTarget)
            {
                this.MappingLeftOfTarget = mappingLeftOfTarget ?? throw new ArgumentNullException(nameof(mappingLeftOfTarget));
                this.TargetMapping = targetMapping ?? throw new ArgumentNullException(nameof(targetMapping));
                this.MappingRightOfTarget = mappingRightOfTarget ?? throw new ArgumentNullException(nameof(mappingRightOfTarget));
            }

            public IReadOnlyDictionary<FeedRangeEpkRange, T> MappingLeftOfTarget { get; }
            public IReadOnlyDictionary<FeedRangeEpkRange, T> TargetMapping { get; }
            public IReadOnlyDictionary<FeedRangeEpkRange, T> MappingRightOfTarget { get; }
        }
    }
}
