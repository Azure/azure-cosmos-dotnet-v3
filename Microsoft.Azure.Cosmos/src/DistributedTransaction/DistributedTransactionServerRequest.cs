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
        }

        public IReadOnlyList<DistributedTransactionOperation> Operations { get; }

        /// <summary>
        /// The idempotency token for the current attempt, <see cref="Guid.Empty"/> until the first
        /// <see cref="RotateIdempotencyToken"/>. It rotates for each new logical attempt (first attempt or
        /// a post-Abort resubmission) and is replayed for a non-aborted retriable retry; the serialized
        /// body is decoupled and reused byte-for-byte either way.
        /// </summary>
        public Guid IdempotencyToken { get; private set; }

        /// <summary>
        /// Assigns a fresh <see cref="Guid"/> to <see cref="IdempotencyToken"/> and returns it. Called for
        /// each new logical attempt (first attempt or a post-Abort resubmission); a non-aborted retriable
        /// retry reuses the current token instead.
        /// </summary>
        /// <returns>The newly generated idempotency token.</returns>
        public Guid RotateIdempotencyToken()
        {
            this.IdempotencyToken = Guid.NewGuid();
            return this.IdempotencyToken;
        }

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
