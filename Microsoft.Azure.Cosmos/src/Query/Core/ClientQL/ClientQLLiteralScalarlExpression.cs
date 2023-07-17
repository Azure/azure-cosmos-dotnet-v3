//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLLiteralScalarlExpression : ClientQLScalarExpression
    {
        public ClientQLLiteral Literal { get; set; }
    }

}