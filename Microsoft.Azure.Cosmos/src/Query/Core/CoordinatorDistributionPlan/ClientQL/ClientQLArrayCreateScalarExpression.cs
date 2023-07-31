//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLArrayCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLArrayKind ArrayKind { get; set; }
        
        public List<ClientQLScalarExpression> VecItems { get; set; }
    }

}