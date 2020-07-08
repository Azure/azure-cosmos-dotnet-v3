// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;

    internal sealed class CrossPartitionState<TState> : State
        where TState : State
    {
        public CrossPartitionState(IReadOnlyList<(FeedRangeInternal, TState)> value)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IReadOnlyList<(FeedRangeInternal, TState)> Value { get; }
    }
}
