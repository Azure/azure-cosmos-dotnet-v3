//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLVariable
    {
        public ClientQLVariable(string name, int uniqueId)
        {
            this.Name = name;
            this.UniqueId = uniqueId;
        }

        public string Name { get; }
        
        public int UniqueId { get; }
    }
}