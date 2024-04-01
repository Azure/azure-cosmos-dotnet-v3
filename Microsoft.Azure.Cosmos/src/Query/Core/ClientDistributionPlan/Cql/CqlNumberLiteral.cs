//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System.Collections.Generic;

    internal class CqlNumberLiteral : CqlLiteral
    {
        public CqlNumberLiteral(Number64 value)
            : base(CqlLiteralKind.Number)
        {
            this.Value = value;
        }

        public Number64 Value { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}