//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    internal class PartitionKeyRangeBatchExecutionResult
    {
        public string PartitionKeyRangeId { get; }

        public List<BatchResponse> ServerResponses { get; }

        public List<ItemBatchOperation> PendingOperations { get; set; }

        public PartitionKeyRangeBatchExecutionResult(string pkRangeId, List<BatchResponse> serverResponses, List<ItemBatchOperation> pendingOperations = null)
        {
            this.PartitionKeyRangeId = pkRangeId;
            this.ServerResponses = serverResponses;
            this.PendingOperations = pendingOperations;
        }
    }
}