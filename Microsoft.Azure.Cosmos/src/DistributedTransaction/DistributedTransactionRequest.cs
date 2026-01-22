//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a distributed transaction request to be sent to the coordinator.
    /// Maps to SDKToCoordinatorDTCRequest in the backend API.
    /// </summary>
    internal class DistributedTransactionRequest
    {
        public DistributedTransactionRequest(
            IReadOnlyList<DistributedTransactionOperation> operations,
            OperationType operationType = OperationType.Batch,
            ResourceType resourceType = ResourceType.Document)
        {
            this.Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.IdempotencyToken = Guid.NewGuid();
            this.OperationType = operationType;
            this.ResourceType = resourceType;
            this.Headers = new Dictionary<string, string>();
        }

        public Guid IdempotencyToken { get; set; }

        public OperationType OperationType { get; set; }

        public ResourceType ResourceType { get; set; }

        public IReadOnlyList<DistributedTransactionOperation> Operations { get; set; }

        public Dictionary<string, string> Headers { get; set; }
    }
}