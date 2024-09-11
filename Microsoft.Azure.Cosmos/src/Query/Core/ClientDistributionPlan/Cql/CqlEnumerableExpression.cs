//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal abstract class CqlEnumerableExpression
    {
        protected CqlEnumerableExpression(CqlEnumerableExpressionKind kind)
        {
            this.Kind = kind;
        }

        public CqlEnumerableExpressionKind Kind { get; }

        public abstract void Accept(ICqlVisitor cqlVisitor);
    }
}