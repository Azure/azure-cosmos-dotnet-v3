﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLUndefinedLiteral : ClientQLLiteral
    {
        public ClientQLUndefinedLiteral()
            : base(ClientQLLiteralKind.Undefined)
        {
        }
    }
}