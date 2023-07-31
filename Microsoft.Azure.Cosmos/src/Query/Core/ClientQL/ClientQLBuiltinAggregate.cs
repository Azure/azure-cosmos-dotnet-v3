//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLBuiltinAggregate : ClientQLAggregate
    {
        public ClientQLAggregateOperatorKind OperatorKind { get; set; }
    }
}