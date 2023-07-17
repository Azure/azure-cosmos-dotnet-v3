//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLScalarExpression : ClientQLExpression
    {
        public ClientQLEnumerableExpressionKind Kind { get; set; }
    }
}