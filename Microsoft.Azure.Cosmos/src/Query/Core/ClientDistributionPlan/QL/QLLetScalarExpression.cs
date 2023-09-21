//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLLetScalarExpression : QLScalarExpression
    {
        public QLLetScalarExpression(QLVariable declaredVariable, QLScalarExpression declaredVariableExpression, QLScalarExpression expression) 
            : base(QLScalarExpressionKind.Let)
        {
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.DeclaredVariableExpression = declaredVariableExpression ?? throw new ArgumentNullException(nameof(declaredVariableExpression));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public QLVariable DeclaredVariable { get; }

        public QLScalarExpression DeclaredVariableExpression { get; }
        
        public QLScalarExpression Expression { get; }
    }
}