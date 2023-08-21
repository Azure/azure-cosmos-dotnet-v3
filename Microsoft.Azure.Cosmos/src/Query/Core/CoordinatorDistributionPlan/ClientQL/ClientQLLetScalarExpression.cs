//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLLetScalarExpression : ClientQLScalarExpression
    {
        public ClientQLLetScalarExpression(ClientQLVariable declaredVariable, ClientQLScalarExpression declaredVariableExpression, ClientQLScalarExpression expression) 
            : base(ClientQLScalarExpressionKind.Let)
        {
            this.DeclaredVariable = declaredVariable;
            this.DeclaredVariableExpression = declaredVariableExpression;
            this.Expression = expression;
        }

        public ClientQLVariable DeclaredVariable { get; }

        public ClientQLScalarExpression DeclaredVariableExpression { get; }
        
        public ClientQLScalarExpression Expression { get; }
    }

}