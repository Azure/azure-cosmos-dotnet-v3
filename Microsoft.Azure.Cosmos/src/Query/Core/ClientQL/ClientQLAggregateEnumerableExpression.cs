//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System;

    internal class ClientQLAggregateEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLEnumerableExpression SourceExpression { get; set; }
        public ClientQLAggregate Aggregate { get; set; }
    }

}