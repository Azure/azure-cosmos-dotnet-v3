//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLAggregateEnumerableExpression : QLEnumerableExpression
    {
        public QLAggregateEnumerableExpression(QLEnumerableExpression sourceExpression, QLAggregate aggregate) 
            : base(QLEnumerableExpressionKind.Aggregate)
        {
            this.SourceExpression = sourceExpression;
            this.Aggregate = aggregate;
        }

        public QLEnumerableExpression SourceExpression { get; }
        
        public QLAggregate Aggregate { get; }
    }
}