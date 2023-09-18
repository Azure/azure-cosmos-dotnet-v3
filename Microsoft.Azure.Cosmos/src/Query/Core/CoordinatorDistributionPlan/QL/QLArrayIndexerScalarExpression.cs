//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLArrayIndexerScalarExpression : QLScalarExpression
    {
        public QLArrayIndexerScalarExpression(QLScalarExpression expression, long index) 
            : base(QLScalarExpressionKind.ArrayIndexer)
        {
            this.Expression = expression;
            this.Index = index;
        }

        public QLScalarExpression Expression { get; }
        
        public long Index { get; }
    }

}