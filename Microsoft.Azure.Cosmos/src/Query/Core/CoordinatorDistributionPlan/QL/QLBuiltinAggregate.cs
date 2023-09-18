//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLBuiltinAggregate : QLAggregate
    {
        public QLBuiltinAggregate(QLAggregateOperatorKind operatorKind)
            : base(QLAggregateKind.Builtin)
        {
            this.OperatorKind = operatorKind;
        }

        public QLAggregateOperatorKind OperatorKind { get; }
    }
}