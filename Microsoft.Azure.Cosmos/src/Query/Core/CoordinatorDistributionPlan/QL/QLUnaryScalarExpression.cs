//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLUnaryScalarExpression : QLScalarExpression
    {
        public QLUnaryScalarExpression(QLUnaryScalarOperatorKind operatorKind, QLScalarExpression expression) 
            : base(QLScalarExpressionKind.UnaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression;
        }

        public QLUnaryScalarOperatorKind OperatorKind { get; }
        
        public QLScalarExpression Expression { get; }
    }
}