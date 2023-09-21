//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLNumberLiteral : QLLiteral
    {
        public QLNumberLiteral(Number64 value)
            : base(QLLiteralKind.Number)
        {
            this.Value = value;
        }

        public Number64 Value { get; }
    }
}