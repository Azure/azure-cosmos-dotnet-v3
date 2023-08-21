//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLStringLiteral : ClientQLLiteral
    {
        public ClientQLStringLiteral(string strValue)
            : base(ClientQLLiteralKind.String)
        {
            this.StrValue = strValue;
        }

        public string StrValue { get; }
    }
}