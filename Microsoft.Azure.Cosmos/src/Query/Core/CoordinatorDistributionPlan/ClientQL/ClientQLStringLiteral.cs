//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLStringLiteral : ClientQLLiteral
    {
        public ClientQLStringLiteral(string value)
            : base(ClientQLLiteralKind.String)
        {
            this.Value = value;
        }

        public string Value { get; }
    }
}