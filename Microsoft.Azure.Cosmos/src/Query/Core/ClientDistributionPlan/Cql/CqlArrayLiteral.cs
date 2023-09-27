//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlArrayLiteral : CqlLiteral
    {
        public CqlArrayLiteral(IReadOnlyList<CqlLiteral> items)
            : base(CqlLiteralKind.Array)
        {
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<CqlLiteral> Items { get; }
    }
}