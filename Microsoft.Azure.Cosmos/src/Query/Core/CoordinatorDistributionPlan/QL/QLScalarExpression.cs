//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal abstract class QLScalarExpression
    {
        protected QLScalarExpression(QLScalarExpressionKind kind)
        {
            this.Kind = kind;
        }

        public QLScalarExpressionKind Kind { get; }
    }
}