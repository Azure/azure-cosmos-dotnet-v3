//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLScalarExpressionDelegate : ClientQLDelegate
    {
        public ClientQLScalarExpressionDelegate(ClientQLType type, ClientQLVariable declaredVariable, ClientQLScalarExpression expression)
            : base(ClientQLDelegateKind.ScalarExpression, type)
        {
            this.DeclaredVariable = declaredVariable;
            this.Expression = expression;
        }

        public ClientQLVariable DeclaredVariable { get; }
        
        public ClientQLScalarExpression Expression { get; }
    }
}