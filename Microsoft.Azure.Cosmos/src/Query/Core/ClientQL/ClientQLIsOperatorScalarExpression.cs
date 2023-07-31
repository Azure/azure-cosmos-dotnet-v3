//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLIsOperatorScalarExpression : ClientQLScalarExpression
    {
        public ClientQLIsOperatorKind OperatorKind { get; set; }
        
        public ClientQLScalarExpression Expression { get; set; }
    }

}