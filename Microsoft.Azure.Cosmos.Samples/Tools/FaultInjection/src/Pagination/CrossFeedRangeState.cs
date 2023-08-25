// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;

    internal sealed class CrossFeedRangeState<TState> : State
        where TState : State
    {
        public CrossFeedRangeState(ReadOnlyMemory<FeedRangeState<TState>> value)
        {
            if (value.IsEmpty)
            {
                throw new ArgumentException($"{nameof(value)} is empty.");
            }

            this.Value = value;
        }

        public ReadOnlyMemory<FeedRangeState<TState>> Value { get; }
    }
}
