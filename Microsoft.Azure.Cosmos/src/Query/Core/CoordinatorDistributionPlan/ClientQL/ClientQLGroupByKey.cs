//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLGroupByKey
    {
        public ClientQLGroupByKey(ClientQLType type)
        {
            this.Type = type;
        }

        public ClientQLType Type { get; }
    }
}