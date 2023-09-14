//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLNullLiteral : ClientQLLiteral
    {
        public static readonly ClientQLNullLiteral Singleton = new ClientQLNullLiteral();

        private ClientQLNullLiteral()
            : base(ClientQLLiteralKind.Null)
        {
        }
    }
}