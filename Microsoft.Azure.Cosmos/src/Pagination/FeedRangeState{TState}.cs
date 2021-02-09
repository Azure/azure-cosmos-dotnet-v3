// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif  
        readonly struct FeedRangeState<TState>
        where TState : State
    {
        public FeedRangeState(FeedRangeInternal feedRange, TState state)
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
            this.State = state;
        }

        public FeedRangeInternal FeedRange { get; }

        public TState State { get; }
    }
}
