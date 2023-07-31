//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLObjectLiteralProperty : ClientQLLiteral
    {
        public string StrName { get; set; }

        public ClientQLLiteral Literal { get; set; }
    }
}