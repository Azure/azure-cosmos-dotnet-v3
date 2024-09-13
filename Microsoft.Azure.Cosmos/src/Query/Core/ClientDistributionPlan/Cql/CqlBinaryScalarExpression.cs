//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlBinaryScalarExpression : CqlScalarExpression
    {
        public CqlBinaryScalarExpression(CqlBinaryScalarOperatorKind operatorKind, CqlScalarExpression leftExpression, CqlScalarExpression rightExpression) 
            : base(CqlScalarExpressionKind.BinaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.LeftExpression = leftExpression ?? throw new ArgumentNullException(nameof(leftExpression));
            this.RightExpression = rightExpression ?? throw new ArgumentNullException(nameof(rightExpression));
        }

        public CqlBinaryScalarOperatorKind OperatorKind { get; }

        public CqlScalarExpression LeftExpression { get; }
        
        public CqlScalarExpression RightExpression { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}