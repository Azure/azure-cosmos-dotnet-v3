//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLArrayLiteral : QLLiteral
    {
        public QLArrayLiteral(IReadOnlyList<QLLiteral> items)
            : base(QLLiteralKind.Array)
        {
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<QLLiteral> Items { get; }
    }
}