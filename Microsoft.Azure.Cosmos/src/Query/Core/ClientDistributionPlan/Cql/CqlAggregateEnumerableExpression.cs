//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlAggregateEnumerableExpression : CqlEnumerableExpression
    {
        public CqlAggregateEnumerableExpression(CqlEnumerableExpression sourceExpression, CqlAggregate aggregate) 
            : base(CqlEnumerableExpressionKind.Aggregate)
        { 
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.Aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        }

        public CqlEnumerableExpression SourceExpression { get; }
        
        public CqlAggregate Aggregate { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}