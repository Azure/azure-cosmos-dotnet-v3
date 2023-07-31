//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System;

    internal class ClientQLOrderByEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLEnumerableExpression SourceExpression { get; set; }

        public ClientQLVariable DeclaredVariable { get; set; }
        
        public List<ClientQLOrderByItem> VecItems { get; set; }
    }

}