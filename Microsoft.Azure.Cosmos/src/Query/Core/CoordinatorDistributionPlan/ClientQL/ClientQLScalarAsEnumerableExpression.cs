//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLScalarAsEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLScalarAsEnumerableExpression(ClientQLScalarExpression expression, ClientQLEnumerationKind enumerationKind) 
            : base(ClientQLEnumerableExpressionKind.ScalarAsEnumerable)
        {
            this.Expression = expression;
            this.EnumerationKind = enumerationKind;
        }

        public ClientQLScalarExpression Expression { get; }

        public ClientQLEnumerationKind EnumerationKind { get; }
    }

}