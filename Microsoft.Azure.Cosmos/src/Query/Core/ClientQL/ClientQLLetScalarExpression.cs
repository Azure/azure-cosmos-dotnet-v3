//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLLetScalarExpression : ClientQLScalarExpression
    {
        public ClientQLVariable DeclaredVariable { get; set; }

        public ClientQLScalarExpression DeclaredVariableExpression { get; set; }
        
        public ClientQLScalarExpression Expression { get; set; }
    }

}