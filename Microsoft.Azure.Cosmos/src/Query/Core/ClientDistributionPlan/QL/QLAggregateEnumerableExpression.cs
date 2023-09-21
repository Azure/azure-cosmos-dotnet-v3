//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLAggregateEnumerableExpression : QLEnumerableExpression
    {
        public QLAggregateEnumerableExpression(QLEnumerableExpression sourceExpression, QLAggregate aggregate) 
            : base(QLEnumerableExpressionKind.Aggregate)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.Aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        }

        public QLEnumerableExpression SourceExpression { get; }
        
        public QLAggregate Aggregate { get; }
    }
}