//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLObjectCreateScalarExpression : ClientQLScalarExpression
    {
        public List<ClientQLObjectProperty> Properties { get; set; }
        
        public ClientQLObjectKind ObjectKind { get; set; }
    }

}