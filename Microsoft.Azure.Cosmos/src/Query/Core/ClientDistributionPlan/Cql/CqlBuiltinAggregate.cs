//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal class CqlBuiltinAggregate : CqlAggregate
    {
        public CqlBuiltinAggregate(CqlAggregateOperatorKind operatorKind)
            : base(CqlAggregateKind.Builtin)
        {
            this.OperatorKind = operatorKind;
        }

        public CqlAggregateOperatorKind OperatorKind { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}