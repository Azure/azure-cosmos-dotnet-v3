//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System;

    internal class ClientQLWhereEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLEnumerableExpression SourceExpression { get; set; }
        
        public ClientQLDelegate Delegate { get; set; }
    }

}