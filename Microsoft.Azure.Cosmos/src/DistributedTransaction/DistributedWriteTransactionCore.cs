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
        private readonly CosmosClientContext clientContext;
        private readonly List<DistributedTransactionOperation> operations;

        internal DistributedWriteTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedWriteTransaction CreateItem<T>(
            Container container,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    databaseId,
                    containerId,
                    partitionKey,
                    id,
                    resource,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction CreateItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    database: databaseId,
                    container: containerId,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override DistributedWriteTransaction ReplaceItem<T>(
            Container container,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    databaseId,
                    containerId,
                    partitionKey,
                    id,
                    resource,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction ReplaceItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    database: databaseId,
                    container: containerId,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override DistributedWriteTransaction DeleteItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Delete,
                    operationIndex: this.operations.Count,
                    databaseId,
                    containerId,
                    partitionKey,
                    id: id,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction PatchItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
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
                    databaseId,
                    containerId,
                    partitionKey,
                    id,
                    resource: patchSpec,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction PatchItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    database: databaseId,
                    container: containerId,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override DistributedWriteTransaction UpsertItem<T>(
            Container container,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            DistributedWriteTransactionCore.ValidateResource(resource);

            this.operations.Add(
                new DistributedTransactionOperation<T>(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    databaseId,
                    containerId,
                    partitionKey,
                    id,
                    resource,
                    requestOptions));
            return this;
        }

        public override DistributedWriteTransaction UpsertItemStream(
            Container container,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedWriteTransactionCore.ValidateItemId(id);
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    database: databaseId,
                    container: containerId,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions)
                {
                    ResourceStream = streamPayload
                });
            return this;
        }

        public override async Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken)
        {
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations: this.operations,
                clientContext: this.clientContext);

            return await committer.CommitTransactionAsync(cancellationToken);
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
