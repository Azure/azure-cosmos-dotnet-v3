//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLArrayIndexerScalarExpression : QLScalarExpression
    {
        public QLArrayIndexerScalarExpression(QLScalarExpression expression, ulong index) 
            : base(QLScalarExpressionKind.ArrayIndexer)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Index = index;
        }

        public QLScalarExpression Expression { get; }
        
        public ulong Index { get; }
    }
}