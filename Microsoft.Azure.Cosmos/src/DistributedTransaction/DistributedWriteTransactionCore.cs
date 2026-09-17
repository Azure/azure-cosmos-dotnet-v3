// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedWriteTransactionCore : DistributedWriteTransaction
    {
        internal const string CommitAlreadyCalledMessage =
            "ExecuteTransactionAsync has already been called on this transaction instance. " +
            "A DistributedWriteTransaction is single-use because each commit generates a new " +
            "idempotency token; a second call would bypass server-side duplicate detection and " +
            "risk a double-commit. To retry, construct a new DistributedWriteTransaction with " +
            "the same operations. If the previous commit's outcome is unknown (e.g., cancellation " +
            "or network failure), verify the resulting state before retrying to avoid duplicate writes.";

        private readonly CosmosClientContext clientContext;
        private readonly DistributedTransactionOperationBuffer operationBuffer;
        private readonly object idempotencyTokenLock = new object();
        private Guid latestIdempotencyToken;

        internal DistributedWriteTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operationBuffer = new DistributedTransactionOperationBuffer(
                CommitAlreadyCalledMessage,
                "Cannot commit a distributed write transaction with zero operations. This instance is consumed; construct a new DistributedWriteTransaction and add at least one write operation before executing.");
        }

        /// <inheritdoc/>
        internal override Guid IdempotencyToken
        {
            get
            {
                lock (this.idempotencyTokenLock)
                {
                    return this.latestIdempotencyToken;
                }
            }
        }

        public override DistributedWriteTransaction CreateItem<T>(
            Container container,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operationBuffer.AddOperation(
                operationType: OperationType.Create,
                databaseId,
                containerId,
                partitionKey,
                id,
                resource,
                requestOptions);
            return this;
        }

        public override DistributedWriteTransaction CreateItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operationBuffer.AddOperation(
                operationType: OperationType.Create,
                databaseId,
                containerId,
                partitionKey,
                id,
                requestOptions: requestOptions,
                streamPayload: streamPayload);
            return this;
        }

        public override DistributedWriteTransaction ReplaceItem<T>(
            Container container,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operationBuffer.AddOperation(
                operationType: OperationType.Replace,
                databaseId,
                containerId,
                partitionKey,
                id,
                resource,
                requestOptions);
            return this;
        }

        public override DistributedWriteTransaction ReplaceItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operationBuffer.AddOperation(
                operationType: OperationType.Replace,
                databaseId,
                containerId,
                partitionKey,
                id,
                requestOptions: requestOptions,
                streamPayload: streamPayload);
            return this;
        }

        public override DistributedWriteTransaction DeleteItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);

            this.operationBuffer.AddOperation(
                operationType: OperationType.Delete,
                databaseId,
                containerId,
                partitionKey,
                id,
                requestOptions: requestOptions);
            return this;
        }

        public override DistributedWriteTransaction PatchItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            DistributedTransactionPatchItemRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);

            if (patchOperations == null || !patchOperations.Any())
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }

            // Forward the customer-supplied conditional predicate (if any) into the PatchSpec so the
            // serializer emits the server-evaluated 'condition' field. Other request-level options
            // (SessionToken, IfMatchEtag, IfNoneMatchEtag) flow through the operation's requestOptions.
            PatchSpec patchSpec = new PatchSpec(
                patchOperations,
                new PatchItemRequestOptions { FilterPredicate = requestOptions?.FilterPredicate });

            this.operationBuffer.AddOperation(
                operationType: OperationType.Patch,
                databaseId,
                containerId,
                partitionKey,
                id,
                resource: patchSpec,
                requestOptions);
            return this;
        }

        public override DistributedWriteTransaction PatchItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operationBuffer.AddOperation(
                operationType: OperationType.Patch,
                databaseId,
                containerId,
                partitionKey,
                id,
                requestOptions: requestOptions,
                streamPayload: streamPayload);
            return this;
        }

        public override DistributedWriteTransaction UpsertItem<T>(
            Container container,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operationBuffer.AddOperation(
                operationType: OperationType.Upsert,
                databaseId,
                containerId,
                partitionKey,
                id,
                resource,
                requestOptions);
            return this;
        }

        public override DistributedWriteTransaction UpsertItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operationBuffer.AddOperation(
                operationType: OperationType.Upsert,
                databaseId,
                containerId,
                partitionKey,
                id,
                requestOptions: requestOptions,
                streamPayload: streamPayload);
            return this;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Each call to <see cref="DistributedTransaction.ExecuteTransactionAsync"/> generates a unique
        /// idempotency token that the server uses for duplicate detection during the SDK's internal
        /// retries. A second call would generate a new token and bypass that server-side duplicate
        /// detection, risking a double-commit. When the previous commit's outcome is unknown
        /// (e.g., cancellation or network failure), verify the resulting state before retrying
        /// to avoid duplicate writes.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the transaction has no operations or <see cref="DistributedTransaction.ExecuteTransactionAsync"/> has already been called on this instance.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled before or during the commit.</exception>
        public override Task<DistributedTransactionResponse> ExecuteTransactionAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DistributedTransactionOperation> operations = this.operationBuffer.FreezeForExecution();

            return this.clientContext.OperationHelperAsync(
                operationName: $"{nameof(DistributedWriteTransaction)}.{nameof(ExecuteTransactionAsync)}",
                containerName: null,
                databaseName: null,
                operationType: OperationType.CommitDistributedTransaction,
                requestOptions: null,
                task: (trace) =>
                {
                    DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                        operations: operations,
                        clientContext: this.clientContext,
                        operationType: OperationType.CommitDistributedTransaction,
                        onDispatch: this.PublishIdempotencyToken);

                    return committer.ExecuteTransactionAsync(trace, cancellationToken);
                },
                openTelemetry: new (OpenTelemetryConstants.Operations.ExecuteDistributedWriteTransaction,
                                    (response) => new OpenTelemetryResponse(response)),
                traceComponent: TraceComponent.Batch);
        }

        private void PublishIdempotencyToken(Guid token)
        {
            lock (this.idempotencyTokenLock)
            {
                this.latestIdempotencyToken = token;
            }
        }

        private static void ValidateItemId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
        }

        private static void ValidateResource<T>(T resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
        }
    }
}
