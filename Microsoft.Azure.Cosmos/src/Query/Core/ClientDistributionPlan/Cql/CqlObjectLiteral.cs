//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlObjectLiteral : CqlLiteral
    {
        public CqlObjectLiteral(IReadOnlyList<CqlObjectLiteralProperty> properties)
            : base(CqlLiteralKind.Object)
        {
            this.Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public IReadOnlyList<CqlObjectLiteralProperty> Properties { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}