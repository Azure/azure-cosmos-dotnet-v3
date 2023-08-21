//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLAggregateEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLAggregateEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLAggregate aggregate) 
            : base(ClientQLEnumerableExpressionKind.Aggregate)
        {
            this.SourceExpression = sourceExpression;
            this.Aggregate = aggregate;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }
        
        public ClientQLAggregate Aggregate { get; }
    }

}