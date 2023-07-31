//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLObjectProperty
    {
        public string Name { get; set; }
        
        public ClientQLScalarExpression Expression { get; set; }
    }
}