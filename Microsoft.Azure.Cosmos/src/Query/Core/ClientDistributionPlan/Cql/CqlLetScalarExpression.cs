//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlLetScalarExpression : CqlScalarExpression
    {
        public CqlLetScalarExpression(CqlVariable declaredVariable, CqlScalarExpression declaredVariableExpression, CqlScalarExpression expression) 
            : base(CqlScalarExpressionKind.Let)
        {
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.DeclaredVariableExpression = declaredVariableExpression ?? throw new ArgumentNullException(nameof(declaredVariableExpression));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public CqlVariable DeclaredVariable { get; }

        public CqlScalarExpression DeclaredVariableExpression { get; }
        
        public CqlScalarExpression Expression { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}