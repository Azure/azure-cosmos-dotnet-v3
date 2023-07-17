//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLEnumerableExpression : ClientQLExpression
    {
        public ClientQLEnumerableExpressionKind Kind { get; set; }
    }
}