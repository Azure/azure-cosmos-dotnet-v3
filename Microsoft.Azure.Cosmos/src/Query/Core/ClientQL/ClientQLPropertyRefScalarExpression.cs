//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLPropertyRefScalarExpression : ClientQLScalarExpression
    {
        public ClientQLScalarExpression Expression { get; set; }
        public string PropertyName { get; set; } //might need to be changed
    }

}