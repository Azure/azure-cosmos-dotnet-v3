//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal class QLCNumberLiteral : QLLiteral
    {
        public QLCNumberLiteral(long value)
            : base(QLLiteralKind.CNumber)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}