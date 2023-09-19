//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLSelectManyEnumerableExpression : QLEnumerableExpression
    {
        public QLSelectManyEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, QLEnumerableExpression selectorExpression)
            : base(QLEnumerableExpressionKind.SelectMany)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.SelectorExpression = selectorExpression;
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }
        
        public QLEnumerableExpression SelectorExpression { get; }
    }
}