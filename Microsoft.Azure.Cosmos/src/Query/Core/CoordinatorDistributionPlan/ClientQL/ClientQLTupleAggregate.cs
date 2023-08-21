//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleAggregate : ClientQLAggregate
    {
        public ClientQLTupleAggregate(string operatorKind, List<ClientQLAggregate> vecItems) 
            : base(ClientQLAggregateKind.Tuple, operatorKind)
        {
            this.VecItems = vecItems;
        }

        public List<ClientQLAggregate> VecItems { get; }
    }
}