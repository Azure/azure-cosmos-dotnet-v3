//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Net;

    internal sealed class PartitionKeyRangeBatchExecutionResult
    {
        public string PartitionKeyRangeId { get; }

        public TransactionalBatchResponse ServerResponse { get; }

        public IEnumerable<ItemBatchOperation> Operations { get; }

        public PartitionKeyRangeBatchExecutionResult(
            string pkRangeId,
            IEnumerable<ItemBatchOperation> operations,
            TransactionalBatchResponse serverResponse)
        {
            this.PartitionKeyRangeId = pkRangeId;
            this.ServerResponse = serverResponse;
            this.Operations = operations;
        }

        internal bool IsSplit() => this.ServerResponse != null &&
                                            this.ServerResponse.StatusCode == HttpStatusCode.Gone
                                                && (this.ServerResponse.SubStatusCode == Documents.SubStatusCodes.CompletingSplit
                                                || this.ServerResponse.SubStatusCode == Documents.SubStatusCodes.CompletingPartitionMigration
                                                || this.ServerResponse.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone);
    }
}