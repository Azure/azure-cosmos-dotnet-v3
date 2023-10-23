//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlUnaryScalarExpression : CqlScalarExpression
    {
        public CqlUnaryScalarExpression(CqlUnaryScalarOperatorKind operatorKind, CqlScalarExpression expression) 
            : base(CqlScalarExpressionKind.UnaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public CqlUnaryScalarOperatorKind OperatorKind { get; }
        
        public CqlScalarExpression Expression { get; }
    }
}