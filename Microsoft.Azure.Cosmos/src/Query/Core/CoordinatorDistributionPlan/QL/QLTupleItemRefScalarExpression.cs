//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLTupleItemRefScalarExpression : QLScalarExpression
    {
        public QLTupleItemRefScalarExpression(QLScalarExpression expression, ulong index) 
            : base(QLScalarExpressionKind.TupleItemRef)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Index = index;
        }

        public QLScalarExpression Expression { get; }
        
        public ulong Index { get; }
    }
}