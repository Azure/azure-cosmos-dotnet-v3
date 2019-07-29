//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Maintains a batch of operations and dispatches the batch through an executor. Maps results into the original operation contexts.
    /// </summary>
    /// <seealso cref="BatchAsyncOperationContext"/>
    internal class BatchAsyncBatcher : IDisposable
    {
        private readonly SemaphoreSlim tryAddLimiter;
        private readonly CosmosSerializer CosmosSerializer;
        private readonly List<BatchAsyncOperationContext> batchOperations;
        private readonly Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<CrossPartitionKeyBatchResponse>> executor;
        private readonly int maxBatchByteSize;
        private readonly int maxBatchOperationCount;
        private long currentSize = 0;

        public bool IsEmpty => this.batchOperations.Count == 0;

        public BatchAsyncBatcher(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            CosmosSerializer cosmosSerializer,
            Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<CrossPartitionKeyBatchResponse>> executor)
        {
            if (maxBatchOperationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchOperationCount));
            }

            if (maxBatchByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchByteSize));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            this.batchOperations = new List<BatchAsyncOperationContext>(maxBatchOperationCount);
            this.tryAddLimiter = new SemaphoreSlim(1, 1);
            this.executor = executor;
            this.maxBatchByteSize = maxBatchByteSize;
            this.maxBatchOperationCount = maxBatchOperationCount;
            this.CosmosSerializer = cosmosSerializer;
        }

        public bool TryAdd(BatchAsyncOperationContext batchAsyncOperation)
        {
            if (batchAsyncOperation == null)
            {
                throw new ArgumentNullException(nameof(batchAsyncOperation));
            }

            this.tryAddLimiter.Wait();
            try
            {
                if (this.batchOperations.Count == this.maxBatchOperationCount)
                {
                    return false;
                }

                int itemByteSize = batchAsyncOperation.Operation.GetApproximateSerializedLength();

                if (itemByteSize + this.currentSize > this.maxBatchByteSize)
                {
                    return false;
                }

                this.currentSize += itemByteSize;

                // Operation index is in the scope of the current batch
                batchAsyncOperation.Operation.OperationIndex = this.batchOperations.Count;
                this.batchOperations.Add(batchAsyncOperation);
                return true;
            }
            finally
            {
                this.tryAddLimiter.Release();
            }
        }

        public async Task DispatchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.tryAddLimiter.WaitAsync(cancellationToken);

            try
            {
                CrossPartitionKeyBatchResponse batchResponse = await this.executor(this.batchOperations, cancellationToken);

                // If the batch was not successful, we need to set all the responses
                if (!batchResponse.IsSuccessStatusCode)
                {
                    BatchOperationResult errorResult = new BatchOperationResult(batchResponse.StatusCode);
                    foreach (BatchAsyncOperationContext operation in this.batchOperations)
                    {
                        operation.Complete(errorResult);
                    }

                    return;
                }

                for (int index = 0; index < this.batchOperations.Count; index++)
                {
                    BatchAsyncOperationContext operation = this.batchOperations[index];
                    BatchOperationResult response = batchResponse[index];
                    operation.Complete(response);
                }
            }
            catch (Exception ex)
            {
                // Exceptions happening during execution fail all the Tasks
                foreach (BatchAsyncOperationContext operation in this.batchOperations)
                {
                    operation.Fail(ex);
                }
            }
            finally
            {
                this.tryAddLimiter.Release();
                this.Dispose();
            }
        }

        public void Dispose()
        {
            this.tryAddLimiter?.Dispose();
        }
    }
}
