﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMNumberLiteral : ClientQLLiteral
    {
        public int Value { get; set; } // might need to be changed MNumber
    }
}