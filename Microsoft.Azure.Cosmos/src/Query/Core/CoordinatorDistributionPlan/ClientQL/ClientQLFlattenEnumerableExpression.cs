//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLFlattenEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLFlattenEnumerableExpression(ClientQLEnumerableExpression sourceExpression) 
            : base(ClientQLEnumerableExpressionKind.Flatten)
        {
            this.SourceExpression = sourceExpression;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }
    }

}