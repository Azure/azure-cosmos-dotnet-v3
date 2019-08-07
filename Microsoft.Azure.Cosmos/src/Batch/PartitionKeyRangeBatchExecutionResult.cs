//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    internal class PartitionKeyRangeBatchExecutionResult
    {
        public string PartitionKeyRangeId { get; }

        public IReadOnlyList<BatchResponse> ServerResponses { get; }

        public IEnumerable<ItemBatchOperation> Operations { get; }

        public PartitionKeyRangeBatchExecutionResult(
            string pkRangeId,
            IEnumerable<ItemBatchOperation> operations,
            List<BatchResponse> serverResponses)
        {
            this.PartitionKeyRangeId = pkRangeId;
            this.ServerResponses = serverResponses;
            this.Operations = operations;
        }

        internal bool ContainsSplit() => this.ServerResponses != null && this.ServerResponses.Any(serverResponse =>
                                            serverResponse.StatusCode == HttpStatusCode.Gone
                                                && (serverResponse.SubStatusCode == Documents.SubStatusCodes.CompletingSplit
                                                || serverResponse.SubStatusCode == Documents.SubStatusCodes.CompletingPartitionMigration
                                                || serverResponse.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone));
    }
}