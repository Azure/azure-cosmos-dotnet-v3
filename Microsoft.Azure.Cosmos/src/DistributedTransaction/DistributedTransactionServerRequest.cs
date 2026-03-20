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
        private byte[] serializedBody;

        private DistributedTransactionServerRequest(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializerCore)
        {
            this.Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
            this.IdempotencyToken = Guid.NewGuid();
        }

        public IReadOnlyList<DistributedTransactionOperation> Operations { get; }

        public Guid IdempotencyToken { get; }

        public static async Task<DistributedTransactionServerRequest> CreateAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializerCore,
            CancellationToken cancellationToken)
        {
            DistributedTransactionServerRequest request = new DistributedTransactionServerRequest(operations, serializerCore);
            await request.CreateBodyStreamAsync(cancellationToken);
            return request;
        }

        /// <summary>
        /// Returns a new <see cref="MemoryStream"/> backed by the pre-serialized request bytes.
        /// Each call returns an independent, non-writable stream positioned at offset zero so
        /// that the caller can safely wrap it in a <c>using</c> block and dispose it without
        /// affecting subsequent retry attempts.
        /// </summary>
        /// <returns>Body stream.</returns>
        public MemoryStream CreateBodyStream()
        {
            if (this.serializedBody == null)
            {
                throw new InvalidOperationException("Request body has not been initialized. Use CreateAsync to construct a request.");
            }

            return new MemoryStream(this.serializedBody, writable: false);
        }

        private async Task CreateBodyStreamAsync(CancellationToken cancellationToken)
        {
            foreach (DistributedTransactionOperation operation in this.Operations)
            {
                await operation.MaterializeResourceAsync(this.serializerCore, cancellationToken);
                operation.PartitionKeyJson ??= operation.PartitionKey.ToJsonString();
            }

            using (MemoryStream stream = DistributedTransactionSerializer.SerializeRequest(this.Operations))
            {
                this.serializedBody = stream.ToArray();
            }
        }
    }
}
