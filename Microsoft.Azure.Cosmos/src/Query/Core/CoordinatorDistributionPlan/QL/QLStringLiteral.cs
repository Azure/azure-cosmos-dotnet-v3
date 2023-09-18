//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLStringLiteral : QLLiteral
    {
        public QLStringLiteral(string value)
            : base(QLLiteralKind.String)
        {
            this.Value = value;
        }

        public string Value { get; }
    }
}