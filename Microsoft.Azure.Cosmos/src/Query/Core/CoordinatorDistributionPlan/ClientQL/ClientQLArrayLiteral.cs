//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLArrayLiteral : ClientQLLiteral
    {
        public ClientQLArrayLiteral(IReadOnlyList<ClientQLLiteral> vecItems)
            : base(ClientQLLiteralKind.Array)
        {
            this.VecItems = vecItems;
        }

        public IReadOnlyList<ClientQLLiteral> VecItems { get; }
    }
}