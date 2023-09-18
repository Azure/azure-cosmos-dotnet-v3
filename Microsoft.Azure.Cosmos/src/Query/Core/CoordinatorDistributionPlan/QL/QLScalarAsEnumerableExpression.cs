//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLScalarAsEnumerableExpression : QLEnumerableExpression
    {
        public QLScalarAsEnumerableExpression(QLScalarExpression expression, QLEnumerationKind enumerationKind) 
            : base(QLEnumerableExpressionKind.ScalarAsEnumerable)
        {
            this.Expression = expression;
            this.EnumerationKind = enumerationKind;
        }

        public QLScalarExpression Expression { get; }

        public QLEnumerationKind EnumerationKind { get; }
    }

}