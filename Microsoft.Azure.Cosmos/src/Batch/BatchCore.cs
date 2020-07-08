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

    internal class BatchCore : TransactionalBatch
    {
        private readonly PartitionKey partitionKey;

        private readonly ContainerInternal container;

        private List<ItemBatchOperation> operations;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchCore"/> class.
        /// </summary>
        /// <param name="container">Container that has items on which batch operations are to be performed.</param>
        /// <param name="partitionKey">The partition key for all items in the batch. <see cref="PartitionKey"/>.</param>
        internal BatchCore(
            ContainerInternal container,
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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
                    requestOptions: requestOptions,
                    containerCore: this.container));

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
        public virtual Task<TransactionalBatchResponse> ExecuteAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.container.ClientContext.OperationHelperAsync(
                nameof(ExecuteAsync),
                requestOptions,
                (diagnostics) =>
                {
                    BatchExecutor executor = new BatchExecutor(
                                    container: this.container,
                                    partitionKey: this.partitionKey,
                                    operations: this.operations,
                                    batchOptions: requestOptions,
                                    diagnosticsContext: diagnostics);

                    this.operations = new List<ItemBatchOperation>();
                    return executor.ExecuteAsync(cancellationToken);
                });
        }

        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="patchStream">A <see cref="Stream"/> containing the <see cref="PatchSpecification"/>.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
#if INTERNAL
        public override
#else
        internal
#endif
            TransactionalBatch PatchItemStream(
                string id,
                Stream patchStream,
                TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (patchStream == null)
            {
                throw new ArgumentNullException(nameof(patchStream));
            }

            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: patchStream,
                    requestOptions: requestOptions,
                    containerCore: this.container));

            return this;
        }

        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="patchSpecification">Represents a list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
#if INTERNAL
        public override
#else
        internal
#endif
            TransactionalBatch PatchItem(
                string id,
                PatchSpecification patchSpecification,
                TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (patchSpecification == null)
            {
                throw new ArgumentNullException(nameof(patchSpecification));
            }

            this.operations.Add(new ItemBatchOperation<PatchSpecification>(
                operationType: OperationType.Patch,
                operationIndex: this.operations.Count,
                id: id,
                resource: patchSpecification,
                requestOptions: requestOptions,
                containerCore: this.container));

            return this;
        }
    }
}
