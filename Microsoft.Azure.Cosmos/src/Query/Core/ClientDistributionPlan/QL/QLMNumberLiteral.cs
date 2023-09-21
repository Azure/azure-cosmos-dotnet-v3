//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal class QLMNumberLiteral : QLLiteral
    {
        public QLMNumberLiteral(long value)
            : base(QLLiteralKind.MNumber)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}