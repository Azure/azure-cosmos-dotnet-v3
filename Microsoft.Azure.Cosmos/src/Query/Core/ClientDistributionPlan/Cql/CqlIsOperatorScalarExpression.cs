//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlIsOperatorScalarExpression : CqlScalarExpression
    {
        public CqlIsOperatorScalarExpression(CqlIsOperatorKind operatorKind, CqlScalarExpression expression) 
            : base(CqlScalarExpressionKind.IsOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public CqlIsOperatorKind OperatorKind { get; }
        
        public CqlScalarExpression Expression { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}