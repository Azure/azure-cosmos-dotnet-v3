//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlMuxScalarExpression : CqlScalarExpression
    {
        public CqlMuxScalarExpression(CqlScalarExpression conditionExpression, CqlScalarExpression leftExpression, CqlScalarExpression rightExpression) 
            : base(CqlScalarExpressionKind.Mux)
        {
            this.ConditionExpression = conditionExpression ?? throw new ArgumentNullException(nameof(conditionExpression));
            this.LeftExpression = leftExpression ?? throw new ArgumentNullException(nameof(leftExpression));
            this.RightExpression = rightExpression ?? throw new ArgumentNullException(nameof(rightExpression));
        }

        public CqlScalarExpression ConditionExpression { get; }

        public CqlScalarExpression LeftExpression { get; }
        
        public CqlScalarExpression RightExpression { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}