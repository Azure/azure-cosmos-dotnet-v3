//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBooleanLiteral : ClientQLLiteral
    {
        public ClientQLBooleanLiteral(bool value)
            : base(ClientQLLiteralKind.Boolean)
        {
            this.Value = value;
        }

        public bool Value { get; }
    }
}