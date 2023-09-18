//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLArrayLiteral : QLLiteral
    {
        public QLArrayLiteral(IReadOnlyList<QLLiteral> items)
            : base(QLLiteralKind.Array)
        {
            this.Items = items;
        }

        public IReadOnlyList<QLLiteral> Items { get; }
    }
}