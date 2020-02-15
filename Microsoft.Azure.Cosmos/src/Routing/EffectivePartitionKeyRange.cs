// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    internal readonly struct EffectivePartitionKeyRange<T>
    {
        public EffectivePartitionKeyRange(EffectivePartitionKey start, EffectivePartitionKey end)
        {
            this.Start = start;
            this.End = end;
        }

        public EffectivePartitionKey Start { get; }
        public EffectivePartitionKey End { get; }
    }
}
