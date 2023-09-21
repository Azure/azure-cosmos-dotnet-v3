//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal class QLMDateTimeLiteral : QLLiteral
    {
        public QLMDateTimeLiteral(long value)
            : base(QLLiteralKind.MDateTime)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}