//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLBinaryScalarExpression : QLScalarExpression
    {
        public QLBinaryScalarExpression(QLBinaryScalarOperatorKind operatorKind, QLScalarExpression leftExpression, QLScalarExpression rightExpression) 
            : base(QLScalarExpressionKind.BinaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.LeftExpression = leftExpression ?? throw new ArgumentNullException(nameof(leftExpression));
            this.RightExpression = rightExpression ?? throw new ArgumentNullException(nameof(rightExpression));
        }

        public QLBinaryScalarOperatorKind OperatorKind { get; }

        public QLScalarExpression LeftExpression { get; }
        
        public QLScalarExpression RightExpression { get; }
    }
}