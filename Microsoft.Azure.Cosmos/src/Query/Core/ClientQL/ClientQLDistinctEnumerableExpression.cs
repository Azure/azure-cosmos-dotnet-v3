//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLDistinctEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLEnumerableExpression SourceExpression { get; set; }
        public ClientQLVariable DeclaredVariable { get; set; }
        public List<ClientQLScalarExpression> VecExpression { get; set; }
    }

}