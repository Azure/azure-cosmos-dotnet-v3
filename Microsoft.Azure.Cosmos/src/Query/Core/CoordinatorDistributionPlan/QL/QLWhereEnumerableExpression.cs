//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLWhereEnumerableExpression : QLEnumerableExpression
    {
        public QLWhereEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, QLScalarExpression expression)
            : base(QLEnumerableExpressionKind.Where)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.Expression = expression;
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }

        public QLScalarExpression Expression { get; }
    }
}