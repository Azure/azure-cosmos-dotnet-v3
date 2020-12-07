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

            // Merge all the tokens that can be merged as much as possible.
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

            // Split the ranges based off the continuation tokens
            HashSet<FeedRangeEpk> remainingRanges = new HashSet<FeedRangeEpk>(mergedFeedRanges);
            List<(FeedRangeEpk, PartitionedToken)> rangeAndTokens = new List<(FeedRangeEpk, PartitionedToken)>();
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
                        rangeAndTokens.Add((overlappingSection, partitionedToken));

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
                rangeAndTokens.Add((remainingRange, default));
            }

            // Segment the ranges based off that:
            ReadOnlyMemory<(FeedRangeEpk range, PartitionedToken token)> sortedRanges = rangeAndTokens
                .OrderBy((rangeAndToken) => rangeAndToken.Item1.Range.Min)
                .ToArray();

            // Find the continuation token for the partition we left off on:
            PartitionedToken firstContinuationToken = tokens
                .OrderBy((partitionedToken) => partitionedToken.Range.Min)
                .First();

            FeedRangeEpk firstContinuationRange = new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: firstContinuationToken.Range.Min,
                    max: firstContinuationToken.Range.Max,
                    isMinInclusive: true,
                    isMaxInclusive: false));

            // Segment based on the target partition
            int? matchedIndex = null;
            for (int i = 0; (i < sortedRanges.Length) && !matchedIndex.HasValue; i++)
            {
                (FeedRangeEpk range, PartitionedToken token) = sortedRanges.Span[i];
                if (range.Equals(firstContinuationRange))
                {
                    matchedIndex = i;
                }
            }

            if (!matchedIndex.HasValue)
            {
                if (feedRanges.Count != 1)
                {
                    return TryCatch<PartitionMapping<PartitionedToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"{RMResources.InvalidContinuationToken} - Could not find continuation token: {firstContinuationToken}"));
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
                    partitionsLeftOfTarget: mappingForPartitionsLeftOfTarget,
                    targetPartition: mappingForTargetPartition,
                    partitionsRightOfTarget: mappingForPartitionsRightOfTarget));
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
