// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;

    internal sealed class CrossFeedRangeState<TState> : State
        where TState : State
    {
        public CrossFeedRangeState(ImmutableArray<FeedRangeState<TState>> value)
        {
            if (value.IsEmpty)
            {
                throw new ArgumentException($"{nameof(value)} is empty.");
            }

            this.Value = value;
        }

        public ImmutableArray<FeedRangeState<TState>> Value { get; }

        public CrossFeedRangeState<TState> Merge(CrossFeedRangeState<TState> other)
        {
            ImmutableArray<FeedRangeState<TState>> mergedStates = this.Value.Concat(other.Value).ToImmutableArray();
            return new CrossFeedRangeState<TState>(mergedStates);
        }

        public bool TrySplit(out (CrossFeedRangeState<TState> left, CrossFeedRangeState<TState> right) children)
        {
            if (this.Value.Length <= 1)
            {
                // TODO: in the future we should be able to split a single feed range state as long as it's not a single point epk range.
                children = default;
                return false;
            }

            ImmutableArray<FeedRangeState<TState>> leftRanges = this.Value.AsMemory().Slice(start: 0, this.Value.Length / 2).ToArray().ToImmutableArray();
            ImmutableArray<FeedRangeState<TState>> rightRanges = this.Value.AsMemory().Slice(start: this.Value.Length / 2).ToArray().ToImmutableArray();
            CrossFeedRangeState<TState> leftCrossFeedRangeStates = new CrossFeedRangeState<TState>(leftRanges);
            CrossFeedRangeState<TState> rightCrossFeedRangeStates = new CrossFeedRangeState<TState>(rightRanges);

            children = (leftCrossFeedRangeStates, rightCrossFeedRangeStates);
            return true;
        }
    }
}
