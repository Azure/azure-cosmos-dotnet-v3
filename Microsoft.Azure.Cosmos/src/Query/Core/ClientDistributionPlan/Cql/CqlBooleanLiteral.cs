//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal class CqlBooleanLiteral : CqlLiteral
    {
        public CqlBooleanLiteral(bool value)
            : base(CqlLiteralKind.Boolean)
        {
            this.Value = value;
        }

        public bool Value { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}