//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLEnumType : ClientQLType
    {
        public ClientQLType ItemType { get; set; }
    }
}