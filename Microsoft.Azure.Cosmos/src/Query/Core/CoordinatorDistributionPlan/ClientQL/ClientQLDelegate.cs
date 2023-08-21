//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLDelegate
    {
        public ClientQLDelegate(ClientQLDelegateKind kind, ClientQLType type)
        {
            this.Kind = kind;
            this.Type = type;
        }

        public ClientQLDelegateKind Kind { get; }
        
        public ClientQLType Type { get; }
    }
}