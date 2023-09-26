//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlArrayIndexerScalarExpression : CqlScalarExpression
    {
        public CqlArrayIndexerScalarExpression(CqlScalarExpression expression, ulong index) 
            : base(CqlScalarExpressionKind.ArrayIndexer)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Index = index;
        }

        public CqlScalarExpression Expression { get; }
        
        public ulong Index { get; }
    }
}