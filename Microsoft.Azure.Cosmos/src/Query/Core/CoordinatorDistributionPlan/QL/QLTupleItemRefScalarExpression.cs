//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLTupleItemRefScalarExpression : QLScalarExpression
    {
        public QLTupleItemRefScalarExpression(QLScalarExpression expression, long index) 
            : base(QLScalarExpressionKind.TupleItemRef)
        {
            this.Expression = expression;
            this.Index = index;
        }

        public QLScalarExpression Expression { get; }
        
        public long Index { get; }
    }

}