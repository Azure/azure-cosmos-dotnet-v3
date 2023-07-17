//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLAggregate
    {
        public ClientQLAggregateKind Kind { get; set; }
        public string OperatorKind { get; set; }
    }
}