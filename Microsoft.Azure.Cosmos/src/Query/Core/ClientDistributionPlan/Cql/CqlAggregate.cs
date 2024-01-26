//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal abstract class CqlAggregate
    {
        protected CqlAggregate(CqlAggregateKind kind)
        {
            this.Kind = kind;
        }

        public CqlAggregateKind Kind { get; }

        public abstract void Accept(ICqlVisitor cqlVisitor);
    }
}