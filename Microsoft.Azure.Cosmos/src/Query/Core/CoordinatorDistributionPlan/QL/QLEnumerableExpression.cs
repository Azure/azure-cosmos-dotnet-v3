//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal abstract class QLEnumerableExpression : QLExpression
    {
        public QLEnumerableExpression(QLEnumerableExpressionKind kind)
        {
            this.Kind = kind;
        }

        public QLEnumerableExpressionKind Kind { get; }
    }
}