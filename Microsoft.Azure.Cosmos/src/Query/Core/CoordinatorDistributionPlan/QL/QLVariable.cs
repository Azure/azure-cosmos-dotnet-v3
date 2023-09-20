//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLVariable
    {
        public QLVariable(string name, long uniqueId)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.UniqueId = uniqueId;
        }

        public string Name { get; }
        
        public long UniqueId { get; }
    }
}