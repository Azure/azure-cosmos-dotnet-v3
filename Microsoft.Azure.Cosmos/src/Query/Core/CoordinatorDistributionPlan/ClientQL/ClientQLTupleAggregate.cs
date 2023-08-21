//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleAggregate : ClientQLAggregate
    {
        public ClientQLTupleAggregate(string operatorKind, IReadOnlyList<ClientQLAggregate> vecItems) 
            : base(ClientQLAggregateKind.Tuple, operatorKind)
        {
            this.VecItems = vecItems;
        }

        public IReadOnlyList<ClientQLAggregate> VecItems { get; }
    }
}