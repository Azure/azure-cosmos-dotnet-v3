//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlOrderByEnumerableExpression : CqlEnumerableExpression
    {
        public CqlOrderByEnumerableExpression(CqlEnumerableExpression sourceExpression, CqlVariable declaredVariable, IReadOnlyList<CqlOrderByItem> items)
            : base(CqlEnumerableExpressionKind.OrderBy)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public CqlEnumerableExpression SourceExpression { get; }

        public CqlVariable DeclaredVariable { get; }
        
        public IReadOnlyList<CqlOrderByItem> Items { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}