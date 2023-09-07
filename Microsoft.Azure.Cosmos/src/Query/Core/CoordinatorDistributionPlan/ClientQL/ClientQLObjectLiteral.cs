//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLObjectLiteral : ClientQLLiteral
    {
        public ClientQLObjectLiteral(IReadOnlyList<ClientQLObjectLiteral> properties)
            : base(ClientQLLiteralKind.Object)
        {
            this.Properties = properties;
        }

        public IReadOnlyList<ClientQLObjectLiteral> Properties { get; }
    }
}