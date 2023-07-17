//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLTupleItemRefScalarExpression : ClientQLScalarExpression
    {
        public ClientQLScalarExpression Expression { get; set; }
        public int Index { get; set; }
    }

}