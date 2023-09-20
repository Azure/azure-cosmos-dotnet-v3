//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLDistinctEnumerableExpression : QLEnumerableExpression
    {
        public QLDistinctEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, IReadOnlyList<QLScalarExpression> expression) 
            : base(QLEnumerableExpressionKind.Distinct)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }
        
        public IReadOnlyList<QLScalarExpression> Expression { get; }
    }
}