//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMSymbolLiteral : ClientQLLiteral
    {
        public ClientQLMSymbolLiteral(string value)
            : base(ClientQLLiteralKind.MSymbol)
        {
            this.Value = value;
        }

        public string Value { get; }
    }
}