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
    using Microsoft.Azure.Documents;

    internal class DistributedWriteTransactionCore : DistributedWriteTransaction
    {
        internal const string CommitAlreadyCalledMessage =
            "CommitTransactionAsync has already been called on this transaction instance. " +
            "A DistributedWriteTransaction is single-use because each commit generates a new " +
            "idempotency token; a second call would bypass server-side duplicate detection and " +
            "risk a double-commit. To retry, construct a new DistributedWriteTransaction with " +
            "the same operations. If the previous commit's outcome is unknown (e.g., cancellation " +
            "or network failure), verify the resulting state before retrying to avoid duplicate writes.";

        private readonly CosmosClientContext clientContext;
        private readonly List<DistributedTransactionOperation> operations;
        private int isCommitInvoked;

        internal DistributedWriteTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedWriteTransaction CreateItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id,
                    resource,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction CreateItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    database: database,
                    container: collection,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override DistributedWriteTransaction ReplaceItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id,
                    resource,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction ReplaceItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    database: database,
                    container: collection,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override DistributedWriteTransaction DeleteItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Delete,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id: id,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction PatchItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);

            if (patchOperations == null || !patchOperations.Any())
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }

            PatchSpec patchSpec = new PatchSpec(patchOperations, new PatchItemRequestOptions());

            this.operations.Add(
                new DistributedTransactionOperation<PatchSpec>(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id,
                    resource: patchSpec,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction PatchItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    database: database,
                    container: collection,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override DistributedWriteTransaction UpsertItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    database,
                    collection,
                    partitionKey,
                    id,
                    resource,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction UpsertItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedWriteTransactionCore.ValidateContainerReference(database, collection);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    database: database,
                    container: collection,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Each call to <see cref="DistributedTransaction.CommitTransactionAsync"/> generates a unique
        /// idempotency token that the server uses for duplicate detection during the SDK's internal
        /// retries. A second call would generate a new token and bypass that server-side duplicate
        /// detection, risking a double-commit. When the previous commit's outcome is unknown
        /// (e.g., cancellation or network failure), verify the resulting state before retrying
        /// to avoid duplicate writes.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="DistributedTransaction.CommitTransactionAsync"/> has already been called on this instance.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled before or during the commit.</exception>
        public override async Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref this.isCommitInvoked, DistributedTransactionConstants.CommitStarted, DistributedTransactionConstants.CommitNotStarted) != DistributedTransactionConstants.CommitNotStarted)
            {
                throw new InvalidOperationException(CommitAlreadyCalledMessage);
            }

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations: this.operations,
                clientContext: this.clientContext);

            return await committer.CommitTransactionAsync(cancellationToken);
        }

        private static void ValidateContainerReference(string database, string collection)
        {
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (string.IsNullOrWhiteSpace(collection))
            {
                throw new ArgumentNullException(nameof(collection));
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
