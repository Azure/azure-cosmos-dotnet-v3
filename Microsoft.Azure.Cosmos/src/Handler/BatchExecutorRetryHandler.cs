//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class BatchExecutorRetryHandler
    {
        private readonly RetryOptions retryOptions;
        private readonly BatchAsyncContainerExecutor batchAsyncContainerExecutor;

        public BatchExecutorRetryHandler(
            CosmosClientContext clientContext,
            BatchAsyncContainerExecutor batchAsyncContainerExecutor)
        {
            this.retryOptions = clientContext.ClientOptions.GetConnectionPolicy().RetryOptions;
            this.batchAsyncContainerExecutor = batchAsyncContainerExecutor;
        }

        public async Task<ResponseMessage> SendAsync(
            ItemBatchOperation itemBatchOperation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IDocumentClientRetryPolicy retryPolicyInstance = new ResourceThrottleRetryPolicy(
                this.retryOptions.MaxRetryAttemptsOnThrottledRequests,
                this.retryOptions.MaxRetryWaitTimeInSeconds);

            return await AbstractRetryHandler.ExecuteHttpRequestAsync(
                    callbackMethod: async () =>
                    {
                        BatchOperationResult operationResult = await this.batchAsyncContainerExecutor.AddAsync(itemBatchOperation, itemRequestOptions);
                        return operationResult.ToResponseMessage();
                    },
                    callShouldRetry: (cosmosResponseMessage, token) =>
                    {
                        return retryPolicyInstance.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
                    },
                    callShouldRetryException: (exception, token) =>
                    {
                        return retryPolicyInstance.ShouldRetryAsync(exception, cancellationToken);
                    },
                    cancellationToken: cancellationToken);
        }
    }
}
