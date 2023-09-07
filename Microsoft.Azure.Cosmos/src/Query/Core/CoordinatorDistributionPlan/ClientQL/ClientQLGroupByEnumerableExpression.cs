//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLGroupByEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLGroupByEnumerableExpression(ClientQLEnumerableExpression sourceExpression, int keyCount, IReadOnlyList<ClientQLAggregate> aggregates) 
            : base(ClientQLEnumerableExpressionKind.GroupBy)
        {
            this.SourceExpression = sourceExpression;
            this.KeyCount = keyCount;
            this.Aggregates = aggregates;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public int KeyCount { get; }
        
        public IReadOnlyList<ClientQLAggregate> Aggregates { get; }
    }

}