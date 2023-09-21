//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal abstract class QLEnumerableExpression
    {
        protected QLEnumerableExpression(QLEnumerableExpressionKind kind)
        {
            this.Kind = kind;
        }

        public QLEnumerableExpressionKind Kind { get; }
    }
}