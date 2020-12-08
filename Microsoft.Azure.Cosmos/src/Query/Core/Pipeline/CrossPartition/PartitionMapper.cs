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
            IReadOnlyList<FeedRangeEpk> feedRanges,
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

            if (tokens.Count > feedRanges.Count)
            {
                throw new ArgumentException($"{nameof(tokens)} can not have more elements than {nameof(feedRanges)}.");
            }

            List<FeedRangeEpk> mergedFeedRanges = MergeRangesWherePossible(feedRanges);
            List<(FeedRangeEpk, PartitionedToken)> splitRangesAndTokens = SplitRangesBasedOffContinuationToken(mergedFeedRanges, tokens);
            FeedRangeEpk targetFeedRange = GetTargetFeedRange(tokens);
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
        private static List<FeedRangeEpk> MergeRangesWherePossible(IReadOnlyList<FeedRangeEpk> feedRanges)
        {
            Stack<(string min, string max)> mergedRanges = new Stack<(string min, string max)>(feedRanges.Count);
            foreach (FeedRangeEpk feedRange in feedRanges.OrderBy(feedRange => feedRange.Range.Min))
            {
                if (mergedRanges.Count == 0)
                {
                    // If the stack is empty, then just add the range to get things started.
                    mergedRanges.Push((feedRange.Range.Min, feedRange.Range.Max));
                }
                else
                {
                    (string min, string max) = mergedRanges.Pop();
                    if (max == feedRange.Range.Min)
                    {
                        // This means that the ranges are consequtive and can be merged.
                        mergedRanges.Push((min, feedRange.Range.Max));
                    }
                    else
                    {
                        // Just push the ranges on seperately 
                        mergedRanges.Push((min, max));
                        mergedRanges.Push((feedRange.Range.Min, feedRange.Range.Max));
                    }
                }
            }

            List<FeedRangeEpk> mergedFeedRanges = mergedRanges
                .Select(range => new FeedRangeEpk(
                    new Documents.Routing.Range<string>(
                        range.min, range.max, isMinInclusive: true, isMaxInclusive: false)))
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
        private static List<(FeedRangeEpk, PartitionedToken)> SplitRangesBasedOffContinuationToken<PartitionedToken>(
            IReadOnlyList<FeedRangeEpk> feedRanges,
            IReadOnlyList<PartitionedToken> tokens)
            where PartitionedToken : IPartitionedToken
        {
            HashSet<FeedRangeEpk> remainingRanges = new HashSet<FeedRangeEpk>(feedRanges);
            List<(FeedRangeEpk, PartitionedToken)> splitRangesAndTokens = new List<(FeedRangeEpk, PartitionedToken)>();
            foreach (PartitionedToken partitionedToken in tokens)
            {
                foreach (FeedRangeEpk feedRange in remainingRanges)
                {
                    bool rightOfStart = (feedRange.Range.Min == string.Empty)
                        || ((partitionedToken.Range.Min != string.Empty) && (partitionedToken.Range.Min.CompareTo(feedRange.Range.Min) >= 0));
                    bool leftOfEnd = (feedRange.Range.Max == string.Empty)
                        || ((partitionedToken.Range.Max != string.Empty) && (partitionedToken.Range.Max.CompareTo(feedRange.Range.Max) <= 0));

                    bool rangeOverlapsToken = rightOfStart && leftOfEnd;
                    if (rangeOverlapsToken)
                    {
                        // Remove the range and split it into 3 sections:
                        remainingRanges.Remove(feedRange);

                        // 1) Left of Token Range
                        if (feedRange.Range.Min != partitionedToken.Range.Min)
                        {
                            FeedRangeEpk leftOfOverlap = new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    min: feedRange.Range.Min,
                                    max: partitionedToken.Range.Min,
                                    isMinInclusive: true,
                                    isMaxInclusive: false));
                            remainingRanges.Add(leftOfOverlap);
                        }

                        // 2) Token Range
                        FeedRangeEpk overlappingSection = new FeedRangeEpk(
                            new Documents.Routing.Range<string>(
                                min: partitionedToken.Range.Min,
                                max: partitionedToken.Range.Max,
                                isMinInclusive: true,
                                isMaxInclusive: false));
                        splitRangesAndTokens.Add((overlappingSection, partitionedToken));

                        // 3) Right of Token Range
                        if (partitionedToken.Range.Max != feedRange.Range.Max)
                        {
                            FeedRangeEpk rightOfOverlap = new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    min: partitionedToken.Range.Max,
                                    max: feedRange.Range.Max,
                                    isMinInclusive: true,
                                    isMaxInclusive: false));
                            remainingRanges.Add(rightOfOverlap);
                        }

                        break;
                    }
                }
            }

            foreach (FeedRangeEpk remainingRange in remainingRanges)
            {
                // Unmatched ranges just match to null tokens
                splitRangesAndTokens.Add((remainingRange, default));
            }

            return splitRangesAndTokens;
        }

        private static FeedRangeEpk GetTargetFeedRange<PartitionedToken>(IReadOnlyList<PartitionedToken> tokens)
            where PartitionedToken : IPartitionedToken
        {
            PartitionedToken firstContinuationToken = tokens
                .OrderBy((partitionedToken) => partitionedToken.Range.Min)
                .First();

            FeedRangeEpk targetFeedRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: firstContinuationToken.Range.Min,
                    max: firstContinuationToken.Range.Max,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            return targetFeedRange;
        }
        
        /// <summary>
        /// Segments the ranges and their tokens into a partition mapping.
        /// </summary>
        private static TryCatch<PartitionMapping<PartitionedToken>> MonadicConstructPartitionMapping<PartitionedToken>(
            IReadOnlyList<(FeedRangeEpk, PartitionedToken)> splitRangesAndTokens,
            IReadOnlyList<PartitionedToken> tokens,
            FeedRangeEpk targetRange)
            where PartitionedToken : IPartitionedToken
        {
            ReadOnlyMemory<(FeedRangeEpk range, PartitionedToken token)> sortedRanges = splitRangesAndTokens
                .OrderBy((rangeAndToken) => rangeAndToken.Item1.Range.Min)
                .ToArray();

            int? matchedIndex = null;
            for (int i = 0; (i < sortedRanges.Length) && !matchedIndex.HasValue; i++)
            {
                (FeedRangeEpk range, PartitionedToken token) = sortedRanges.Span[i];
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
                sortedRanges = new (FeedRangeEpk, PartitionedToken)[] { (sortedRanges.Span[0].range, tokens[0]) };
                matchedIndex = 0;
            }

            ReadOnlyMemory<(FeedRangeEpk, PartitionedToken)> partitionsLeftOfTarget;
            if (matchedIndex.Value == 0)
            {
                partitionsLeftOfTarget = ReadOnlyMemory<(FeedRangeEpk, PartitionedToken)>.Empty;
            }
            else
            {
                partitionsLeftOfTarget = sortedRanges.Slice(start: 0, length: matchedIndex.Value);
            }

            ReadOnlyMemory<(FeedRangeEpk, PartitionedToken)> targetPartition = sortedRanges.Slice(start: matchedIndex.Value, length: 1);

            ReadOnlyMemory<(FeedRangeEpk, PartitionedToken)> partitionsRightOfTarget;
            if (matchedIndex.Value == sortedRanges.Length - 1)
            {
                partitionsRightOfTarget = ReadOnlyMemory<(FeedRangeEpk, PartitionedToken)>.Empty;
            }
            else
            {
                partitionsRightOfTarget = sortedRanges.Slice(start: matchedIndex.Value + 1);
            }

            static Dictionary<FeedRangeEpk, PartitionedToken> CreateMappingFromTuples(ReadOnlySpan<(FeedRangeEpk, PartitionedToken)> rangeAndTokens)
            {
                Dictionary<FeedRangeEpk, PartitionedToken> mappingForPartitions = new Dictionary<FeedRangeEpk, PartitionedToken>();
                foreach ((FeedRangeEpk range, PartitionedToken token) in rangeAndTokens)
                {
                    mappingForPartitions[range] = token;
                }

                return mappingForPartitions;
            }

            // Create the continuation token mapping for each region.
            IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> mappingForPartitionsLeftOfTarget = CreateMappingFromTuples(partitionsLeftOfTarget.Span);
            IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> mappingForTargetPartition = CreateMappingFromTuples(targetPartition.Span);
            IReadOnlyDictionary<FeedRangeEpk, PartitionedToken> mappingForPartitionsRightOfTarget = CreateMappingFromTuples(partitionsRightOfTarget.Span);

            return TryCatch<PartitionMapping<PartitionedToken>>.FromResult(
                new PartitionMapping<PartitionedToken>(
                    mappingLeftOfTarget: mappingForPartitionsLeftOfTarget,
                    targetMapping: mappingForTargetPartition,
                    mappingRightOfTarget: mappingForPartitionsRightOfTarget));
        }

        public readonly struct PartitionMapping<T>
        {
            public PartitionMapping(
                IReadOnlyDictionary<FeedRangeEpk, T> mappingLeftOfTarget,
                IReadOnlyDictionary<FeedRangeEpk, T> targetMapping,
                IReadOnlyDictionary<FeedRangeEpk, T> mappingRightOfTarget)
            {
                this.MappingLeftOfTarget = mappingLeftOfTarget ?? throw new ArgumentNullException(nameof(mappingLeftOfTarget));
                this.TargetMapping = targetMapping ?? throw new ArgumentNullException(nameof(targetMapping));
                this.MappingRightOfTarget = mappingRightOfTarget ?? throw new ArgumentNullException(nameof(mappingRightOfTarget));
            }

            public IReadOnlyDictionary<FeedRangeEpk, T> MappingLeftOfTarget { get; }
            public IReadOnlyDictionary<FeedRangeEpk, T> TargetMapping { get; }
            public IReadOnlyDictionary<FeedRangeEpk, T> MappingRightOfTarget { get; }
        }
    }
}
