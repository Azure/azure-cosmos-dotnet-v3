// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DistributedTransactionServerRequest
    {
        private readonly CosmosSerializerCore serializerCore;
        private MemoryStream bodyStream;

        private DistributedTransactionServerRequest(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializerCore)
        {
            this.Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
        }

        public IReadOnlyList<DistributedTransactionOperation> Operations { get; }

        public Guid IdempotencyToken { get; private set; }

        public static async Task<DistributedTransactionServerRequest> CreateAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializerCore,
            CancellationToken cancellationToken)
        {
            DistributedTransactionServerRequest request = new DistributedTransactionServerRequest(operations, serializerCore);
            await request.CreateBodyStreamAsync(cancellationToken);
            return request;
        }

        public MemoryStream TransferBodyStream()
        {
            MemoryStream bodyStream = this.bodyStream;
            this.bodyStream = null;
            return bodyStream;
        }

        private async Task CreateBodyStreamAsync(CancellationToken cancellationToken)
        {
            foreach (DistributedTransactionOperation operation in this.Operations)
            {
                await operation.MaterializeResourceAsync(this.serializerCore, cancellationToken);
                operation.PartitionKeyJson ??= operation.PartitionKey.ToJsonString();
            }

            // Generate idempotency token for this request
            this.IdempotencyToken = Guid.NewGuid();

            this.bodyStream = DistributedTransactionSerializer.SerializeRequest(this.Operations);
        }
    }
}
