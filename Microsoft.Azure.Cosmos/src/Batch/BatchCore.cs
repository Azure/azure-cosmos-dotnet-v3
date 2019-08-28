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

    internal class BatchCore : Batch
    {
        private readonly PartitionKey partitionKey;

        private readonly ContainerCore container;

        private List<ItemBatchOperation> operations;

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

        public override Batch CreateItem<T>(
            T item,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch CreateItemStream(
            Stream streamPayload,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch ReadItem(
            string id,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch UpsertItem<T>(
            T item,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch UpsertItemStream(
            Stream streamPayload,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch ReplaceItem<T>(
            string id,
            T item,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch ReplaceItemStream(
            string id,
            Stream streamPayload,
            BatchItemRequestOptions requestOptions = null)
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

        public override Batch DeleteItem(
            string id,
            BatchItemRequestOptions requestOptions = null)
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

        public override Task<BatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ExecuteAsync(
                Constants.MaxDirectModeBatchRequestBodySizeInBytes,
                Constants.MaxOperationsInDirectModeBatchRequest,
                requestOptions: null,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">Options that apply to the batch. Used only for EPK routing.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="BatchResponse"/> which contains the completion status and results of each operation.</returns>
        public virtual Task<BatchResponse> ExecuteAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ExecuteAsync(
                Constants.MaxDirectModeBatchRequestBodySizeInBytes,
                Constants.MaxOperationsInDirectModeBatchRequest,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="patchStream">A <see cref="Stream"/> containing the patch specification.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public virtual Batch PatchItemStream(
            string id,
            Stream patchStream,
            BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: patchStream,
                    requestOptions: requestOptions));

            return this;
        }

        internal Task<BatchResponse> ExecuteAsync(
            int maxServerRequestBodyLength,
            int maxServerRequestOperationCount,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            BatchExecutor executor = new BatchExecutor(this.container, this.partitionKey, this.operations, requestOptions, maxServerRequestBodyLength, maxServerRequestOperationCount);
            this.operations = new List<ItemBatchOperation>();
            return executor.ExecuteAsync(cancellationToken);
        }
    }
}
