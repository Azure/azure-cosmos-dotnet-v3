//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal class DistributedTransactionRequest
    {
        public DistributedTransactionRequest(IReadOnlyList<DistributedTransactionOperation> operations)
        {
            //this.ResourceType = resourceType;
            this.Operations = operations;
        }
        public IReadOnlyList<DistributedTransactionOperation> Operations { get; set; }

    }
}