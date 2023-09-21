//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLIsOperatorScalarExpression : QLScalarExpression
    {
        public QLIsOperatorScalarExpression(QLIsOperatorKind operatorKind, QLScalarExpression expression) 
            : base(QLScalarExpressionKind.IsOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public QLIsOperatorKind OperatorKind { get; }
        
        public QLScalarExpression Expression { get; }
    }
}