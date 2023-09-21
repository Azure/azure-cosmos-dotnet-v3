//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLSelectEnumerableExpression : QLEnumerableExpression
    {
        public QLSelectEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, QLScalarExpression expression) 
            : base(QLEnumerableExpressionKind.Select)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.DeclaredVariable = declaredVariable ?? throw new ArgumentNullException(nameof(declaredVariable));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }
        
        public QLScalarExpression Expression { get; }
    }
}