//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLGroupByEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLGroupByEnumerableExpression(ClientQLEnumerableExpression sourceExpression, List<ClientQLGroupByKey> vecKeys, List<ClientQLAggregate> vecAggregates) 
            : base(ClientQLEnumerableExpressionKind.GroupBy)
        {
            this.SourceExpression = sourceExpression;
            this.VecKeys = vecKeys;
            this.VecAggregates = vecAggregates;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public List<ClientQLGroupByKey> VecKeys { get; }
        
        public List<ClientQLAggregate> VecAggregates { get; }
    }

}