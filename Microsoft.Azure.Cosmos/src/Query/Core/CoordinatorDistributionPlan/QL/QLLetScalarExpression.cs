//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLLetScalarExpression : QLScalarExpression
    {
        public QLLetScalarExpression(QLVariable declaredVariable, QLScalarExpression declaredVariableExpression, QLScalarExpression expression) 
            : base(QLScalarExpressionKind.Let)
        {
            this.DeclaredVariable = declaredVariable;
            this.DeclaredVariableExpression = declaredVariableExpression;
            this.Expression = expression;
        }

        public QLVariable DeclaredVariable { get; }

        public QLScalarExpression DeclaredVariableExpression { get; }
        
        public QLScalarExpression Expression { get; }
    }
}