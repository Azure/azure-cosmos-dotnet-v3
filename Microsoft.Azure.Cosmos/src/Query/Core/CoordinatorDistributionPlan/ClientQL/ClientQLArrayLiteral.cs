//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLArrayLiteral : ClientQLLiteral
    {
        public ClientQLArrayLiteral(IReadOnlyList<ClientQLLiteral> items)
            : base(ClientQLLiteralKind.Array)
        {
            this.Items = items;
        }

        public IReadOnlyList<ClientQLLiteral> Items { get; }
    }
}