//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLSelectManyEnumerableExpression : QLEnumerableExpression
    {
        public QLSelectManyEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, QLEnumerableExpression selectorExpression)
            : base(QLEnumerableExpressionKind.SelectMany)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.SelectorExpression = selectorExpression ?? throw new ArgumentNullException(nameof(selectorExpression));
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }
        
        public QLEnumerableExpression SelectorExpression { get; }
    }
}