//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLSelectEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLSelectEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLVariable declaredVariable, ClientQLScalarExpression expression) 
            : base(ClientQLEnumerableExpressionKind.Select)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.Expression = expression;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public ClientQLVariable DeclaredVariable { get; }
        
        public ClientQLScalarExpression Expression { get; }
    }

}