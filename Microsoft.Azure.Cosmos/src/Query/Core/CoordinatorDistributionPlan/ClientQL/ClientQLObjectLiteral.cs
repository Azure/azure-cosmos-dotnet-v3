﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLObjectLiteral : ClientQLLiteral
    {
        public ClientQLObjectLiteral(IReadOnlyList<ClientQLObjectLiteralProperty> properties)
            : base(ClientQLLiteralKind.Object)
        {
            this.Properties = properties;
        }

        public IReadOnlyList<ClientQLObjectLiteralProperty> Properties { get; }
    }
}