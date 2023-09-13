//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleAggregate : ClientQLAggregate
    {
        public ClientQLTupleAggregate(IReadOnlyList<ClientQLAggregate> items) 
            : base(ClientQLAggregateKind.Tuple)
        {
            this.Items = items;
        }

        public IReadOnlyList<ClientQLAggregate> Items { get; }
    }
}