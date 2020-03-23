//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class BatchCore : TransactionalBatch
    {
        private readonly PartitionKey partitionKey;

        private readonly ContainerCore container;

        private readonly List<ItemBatchOperation> operations;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchCore"/> class.
        /// </summary>
        /// <param name="container">Container that has items on which batch operations are to be performed.</param>
        /// <param name="partitionKey">The partition key for all items in the batch. <see cref="PartitionKey"/>.</param>
        internal BatchCore(
            ContainerCore container,
            PartitionKey partitionKey)
        {
            this.container = container;
            this.partitionKey = partitionKey;
            this.operations = new List<ItemBatchOperation>();
        }

        public override TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    resource: item,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch CreateItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    resourceStream: streamPayload,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    resource: item,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch UpsertItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    resourceStream: streamPayload,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch ReplaceItem<T>(
            string id,
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    id: id,
                    resource: item,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch ReplaceItemStream(
            string id,
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: streamPayload,
                    requestOptions: requestOptions));

            return this;
        }

        public override TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Delete,
                    operationIndex: this.operations.Count,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        public override Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ExecuteAsync(
                requestOptions: null,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">Options that apply to the batch. Used only for EPK routing.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="TransactionalBatchResponse"/> which contains the completion status and results of each operation.</returns>
        public Task<TransactionalBatchResponse> ExecuteAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            BatchExecutor executor = new BatchExecutor(
                container: this.container,
                partitionKey: this.partitionKey,
                operations: this.operations,
                batchOptions: requestOptions,
                diagnosticsContext: diagnosticsContext);

            this.operations.Clear();
            return executor.ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="patchStream">A <see cref="Stream"/> containing the patch specification.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        public TransactionalBatch PatchItemStream(
            string id,
            Stream patchStream,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: patchStream,
                    requestOptions: requestOptions));

            return this;
        }
    }
}
