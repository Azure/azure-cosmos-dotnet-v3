//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLMSymbolLiteral : QLLiteral
    {
        public QLMSymbolLiteral(string value)
            : base(QLLiteralKind.MSymbol)
        {
            this.Value = value;
        }

        public string Value { get; }
    }
}