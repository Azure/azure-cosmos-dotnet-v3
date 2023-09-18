//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLNumberLiteral : QLLiteral
    {
        public QLNumberLiteral(long value)
            : base(QLLiteralKind.Number)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}