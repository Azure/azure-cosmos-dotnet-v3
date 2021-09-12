// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;

    internal static class CrossFeedRangeStateSplitterAndMerger
    {
        // TODO: This class can be code generated.

        public static Memory<FeedRangeState<TState>> Merge<TState>(
            ReadOnlyMemory<FeedRangeState<TState>> first,
            ReadOnlyMemory<FeedRangeState<TState>> second)
            where TState : State
        {
            return Merge(first, second, ReadOnlyMemory<FeedRangeState<TState>>.Empty);
        }

        public static Memory<FeedRangeState<TState>> Merge<TState>(
            ReadOnlyMemory<FeedRangeState<TState>> first,
            ReadOnlyMemory<FeedRangeState<TState>> second,
            ReadOnlyMemory<FeedRangeState<TState>> third)
            where TState : State
        {
            FeedRangeState<TState>[] feedRanges = new FeedRangeState<TState>[first.Length + second.Length + third.Length];
            Memory<FeedRangeState<TState>> feedRangesMemory = feedRanges.AsMemory();

            first.CopyTo(feedRangesMemory);
            feedRangesMemory = feedRangesMemory.Slice(start: first.Length);

            second.CopyTo(feedRangesMemory);
            feedRangesMemory = feedRangesMemory.Slice(start: second.Length);

            third.CopyTo(feedRangesMemory);
            feedRangesMemory = feedRangesMemory.Slice(start: third.Length);

            return feedRanges;
        }

        //TODO: create additional varadic Merge methods for allocation free code paths.

        public static Memory<FeedRangeState<TState>> Merge<TState>(IReadOnlyList<ReadOnlyMemory<FeedRangeState<TState>>> ranges)
            where TState : State
        {
            if (ranges == null)
            {
                throw new ArgumentNullException(nameof(ranges));
            }

            int totalLength = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                totalLength += ranges[i].Length;
            }

            FeedRangeState<TState>[] feedRanges = new FeedRangeState<TState>[totalLength];
            Memory<FeedRangeState<TState>> feedRangesSpan = feedRanges.AsMemory();

            for (int i = 0; i < ranges.Count; i++)
            {
                ReadOnlyMemory<FeedRangeState<TState>> range = ranges[i];
                range.CopyTo(feedRangesSpan);

                feedRangesSpan = feedRangesSpan.Slice(start: range.Length);
            }

            return feedRanges;
        }

        public static bool TrySplit<TState>(
            ReadOnlyMemory<FeedRangeState<TState>> rangeToSplit,
            out ReadOnlyMemory<FeedRangeState<TState>> first,
            out ReadOnlyMemory<FeedRangeState<TState>> second)
            where TState : State
        {
            if (rangeToSplit.Length <= 1)
            {
                // TODO: in the future we should be able to split a single feed range state as long as it's not a single point epk range.
                first = default;
                second = default;
                return false;
            }

            ReadOnlyMemory<FeedRangeState<TState>> rangeLeftToSplit = rangeToSplit;

            first = rangeLeftToSplit.Slice(start: 0, length: rangeToSplit.Length / 2);
            rangeLeftToSplit = rangeLeftToSplit.Slice(start: rangeToSplit.Length / 2);

            second = rangeLeftToSplit.Slice(start: 0, length: rangeToSplit.Length / 2);
            rangeLeftToSplit = rangeLeftToSplit.Slice(start: rangeToSplit.Length / 2);

            return true;
        }

        public static bool TrySplit<TState>(
            ReadOnlyMemory<FeedRangeState<TState>> rangeToSplit,
            out ReadOnlyMemory<FeedRangeState<TState>> first,
            out ReadOnlyMemory<FeedRangeState<TState>> second,
            out ReadOnlyMemory<FeedRangeState<TState>> third)
            where TState : State
        {
            if (rangeToSplit.Length <= 1)
            {
                // TODO: in the future we should be able to split a single feed range state as long as it's not a single point epk range.
                first = default;
                second = default;
                third = default;
                return false;
            }

            ReadOnlyMemory<FeedRangeState<TState>> rangeLeftToSplit = rangeToSplit;

            first = rangeLeftToSplit.Slice(start: 0, length: rangeToSplit.Length / 3);
            rangeLeftToSplit = rangeLeftToSplit.Slice(start: rangeToSplit.Length / 3);

            second = rangeLeftToSplit.Slice(start: 0, length: rangeToSplit.Length / 3);
            rangeLeftToSplit = rangeLeftToSplit.Slice(start: rangeToSplit.Length / 3);

            third = rangeLeftToSplit.Slice(start: 0, length: rangeToSplit.Length / 3);
            rangeLeftToSplit = rangeLeftToSplit.Slice(start: rangeToSplit.Length / 3);

            return true;
        }

        public static bool TrySplit<TState>(
            ReadOnlyMemory<FeedRangeState<TState>> rangeToSplit,
            int numPartitions,
            out List<ReadOnlyMemory<FeedRangeState<TState>>> partitions)
            where TState : State
        {
            if (rangeToSplit.Length <= 1)
            {
                // TODO: in the future we should be able to split a single feed range state as long as it's not a single point epk range.
                partitions = default;
                return false;
            }

            ReadOnlyMemory<FeedRangeState<TState>> rangeLeftToSplit = rangeToSplit;
            
            partitions = new List<ReadOnlyMemory<FeedRangeState<TState>>>(numPartitions);
            int partitionLength = rangeToSplit.Length / numPartitions;
            for (int i = 0; i < numPartitions; i++)
            {
                ReadOnlyMemory<FeedRangeState<TState>> partition = rangeLeftToSplit.Slice(start: 0, length: partitionLength);
                rangeLeftToSplit = rangeLeftToSplit.Slice(start: partitionLength);

                partitions.Add(partition);
            }

            return true;
        }
    }
}
