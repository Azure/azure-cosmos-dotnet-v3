//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal abstract class QLAggregate
    {
        protected QLAggregate(QLAggregateKind kind)
        {
            this.Kind = kind;
        }

        public QLAggregateKind Kind { get; }
    }
}