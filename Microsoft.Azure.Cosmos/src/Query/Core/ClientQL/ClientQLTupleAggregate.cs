//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLTupleAggregate : ClientQLAggregate
    {
        public List<ClientQLAggregate> VecItems { get; set; }
    }
}