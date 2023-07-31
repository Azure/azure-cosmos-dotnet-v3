﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLAggregateSignature
    {
        public ClientQLTypeKind ItemType { get; set; }
        
        public ClientQLTypeKind ResultType { get; set; }
    }
}