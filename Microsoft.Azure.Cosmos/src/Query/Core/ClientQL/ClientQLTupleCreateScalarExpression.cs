//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleCreateScalarExpression : ClientQLScalarExpression
    {
        public List<ClientQLScalarExpression> VecItems { get; set; }
    }

}