//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMSymbolLiteral : ClientQLLiteral
    {
        public ClientQLMSymbolLiteral(string strValue)
            : base(ClientQLLiteralKind.MSymbol)
        {
            this.StrValue = strValue;
        }

        public string StrValue { get; }
    }
}