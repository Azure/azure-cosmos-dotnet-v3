//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLWhereEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLWhereEnumerableExpression(ClientQLEnumerableExpression sourceExpression)
            : base(ClientQLEnumerableExpressionKind.Where)
        {
            this.SourceExpression = sourceExpression;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }
    }

}