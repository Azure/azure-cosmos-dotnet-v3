//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlVariableRefScalarExpression : CqlScalarExpression
    {
        public CqlVariableRefScalarExpression(CqlVariable variable)
            : base(CqlScalarExpressionKind.VariableRef)
        {
            this.Variable = variable ?? throw new ArgumentNullException(nameof(variable));
        }

        public CqlVariable Variable { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}