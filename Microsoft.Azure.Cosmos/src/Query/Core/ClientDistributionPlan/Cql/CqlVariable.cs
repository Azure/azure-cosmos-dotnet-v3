//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlVariable
    {
        public CqlVariable(string name, long uniqueId)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.UniqueId = uniqueId;
        }

        public string Name { get; }
        
        public long UniqueId { get; }
    }
}