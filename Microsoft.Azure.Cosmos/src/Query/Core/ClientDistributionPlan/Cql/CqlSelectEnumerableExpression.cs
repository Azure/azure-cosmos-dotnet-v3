//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlSelectEnumerableExpression : CqlEnumerableExpression
    {
        public CqlSelectEnumerableExpression(CqlEnumerableExpression sourceExpression, CqlVariable declaredVariable, CqlScalarExpression expression) 
            : base(CqlEnumerableExpressionKind.Select)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public CqlEnumerableExpression SourceExpression { get; }

        public CqlVariable DeclaredVariable { get; }
        
        public CqlScalarExpression Expression { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}