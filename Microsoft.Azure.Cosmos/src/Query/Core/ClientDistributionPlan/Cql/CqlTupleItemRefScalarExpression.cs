//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlTupleItemRefScalarExpression : CqlScalarExpression
    {
        public CqlTupleItemRefScalarExpression(CqlScalarExpression expression, ulong index) 
            : base(CqlScalarExpressionKind.TupleItemRef)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Index = index;
        }

        public CqlScalarExpression Expression { get; }
        
        public ulong Index { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}