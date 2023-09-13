//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal abstract class ClientQLAggregate
    {
        public ClientQLAggregate(ClientQLAggregateKind kind)
        {
            this.Kind = kind;
        }

        public ClientQLAggregateKind Kind { get; }
    }
}