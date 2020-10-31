// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;

    internal abstract class FeedRangeState<TState>
        where TState : State
    {
        protected static class PropertyNames
        {
            public const string FeedRange = "FeedRange";
            public const string State = "State";
        }

        public FeedRangeState(FeedRangeInternal feedRange, TState state)
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
            this.State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public FeedRangeInternal FeedRange { get; }

        public TState State { get; }
    }
}
