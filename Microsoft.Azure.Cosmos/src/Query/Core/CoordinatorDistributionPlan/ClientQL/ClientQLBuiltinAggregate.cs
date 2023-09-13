//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBuiltinAggregate : ClientQLAggregate
    {
        public ClientQLBuiltinAggregate(ClientQLAggregateOperatorKind operatorKind)
            : base(ClientQLAggregateKind.Builtin)
        {
            this.OperatorKind = operatorKind;
        }

        public ClientQLAggregateOperatorKind OperatorKind { get; }
    }
}