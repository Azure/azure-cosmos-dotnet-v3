//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBuiltinAggregate : ClientQLAggregate
    {
        public ClientQLBuiltinAggregate(string operatorKind, ClientQLAggregateOperatorKind aggregateOperatorKind)
            : base(ClientQLAggregateKind.Builtin, operatorKind)
        {
            this.AggregateOperatorKind = aggregateOperatorKind;
        }

        public ClientQLAggregateOperatorKind AggregateOperatorKind { get; }
    }
}