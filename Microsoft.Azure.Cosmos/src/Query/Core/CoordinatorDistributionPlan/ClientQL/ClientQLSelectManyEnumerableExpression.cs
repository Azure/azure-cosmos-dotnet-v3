//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLSelectManyEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLSelectManyEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLVariable declaredVariable, ClientQLEnumerableExpression selectorExpression)
            : base(ClientQLEnumerableExpressionKind.SelectMany)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.SelectorExpression = selectorExpression;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public ClientQLVariable DeclaredVariable { get; }
        
        public ClientQLEnumerableExpression SelectorExpression { get; }
    }

}