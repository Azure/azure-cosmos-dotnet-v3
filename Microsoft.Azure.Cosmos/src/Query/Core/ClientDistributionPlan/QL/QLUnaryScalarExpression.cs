//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLUnaryScalarExpression : QLScalarExpression
    {
        public QLUnaryScalarExpression(QLUnaryScalarOperatorKind operatorKind, QLScalarExpression expression) 
            : base(QLScalarExpressionKind.UnaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public QLUnaryScalarOperatorKind OperatorKind { get; }
        
        public QLScalarExpression Expression { get; }
    }
}