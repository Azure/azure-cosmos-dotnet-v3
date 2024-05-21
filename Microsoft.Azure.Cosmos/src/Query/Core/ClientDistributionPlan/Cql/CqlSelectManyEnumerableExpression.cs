//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlSelectManyEnumerableExpression : CqlEnumerableExpression
    {
        public CqlSelectManyEnumerableExpression(CqlEnumerableExpression sourceExpression, CqlVariable declaredVariable, CqlEnumerableExpression selectorExpression)
            : base(CqlEnumerableExpressionKind.SelectMany)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.SelectorExpression = selectorExpression ?? throw new ArgumentNullException(nameof(selectorExpression));
        }

        public CqlEnumerableExpression SourceExpression { get; }

        public CqlVariable DeclaredVariable { get; }
        
        public CqlEnumerableExpression SelectorExpression { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}