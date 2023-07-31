//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLDelegate
    {
        public ClientQLDelegateKind Kind { get; set; }
        
        public ClientQLType Type { get; set; }
    }
}