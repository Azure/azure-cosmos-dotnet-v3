// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal sealed class CrossPartitionState<TState> : State
        where TState : State
    {
        public CrossPartitionState(IReadOnlyList<(PartitionKeyRange, TState)> value)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IReadOnlyList<(PartitionKeyRange, TState)> Value { get; }
    }
}
