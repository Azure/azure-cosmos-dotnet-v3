//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLDistinctEnumerableExpression : QLEnumerableExpression
    {
        public QLDistinctEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, IReadOnlyList<QLScalarExpression> expression) 
            : base(QLEnumerableExpressionKind.Distinct)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.Expression = expression;
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }
        
        public IReadOnlyList<QLScalarExpression> Expression { get; }
    }
}