//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLMSymbolLiteral : QLLiteral
    {
        public QLMSymbolLiteral(string value)
            : base(QLLiteralKind.MSymbol)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Value { get; }
    }
}