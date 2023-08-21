//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLLiteral
    {
        public ClientQLLiteral(ClientQLLiteralKind kind)
        {
            this.Kind = kind;
        }

        public ClientQLLiteralKind Kind { get; }
    }
}