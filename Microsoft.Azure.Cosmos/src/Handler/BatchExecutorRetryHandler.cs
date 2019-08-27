//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class BatchExecutorRetryHandler
    {
        private readonly IDocumentClientRetryPolicy retryPolicyInstance;
        private readonly BatchAsyncContainerExecutor batchAsyncContainerExecutor;

        public BatchExecutorRetryHandler(
            CosmosClientContext clientContext,
            BatchAsyncContainerExecutor batchAsyncContainerExecutor)
        {
            RetryOptions retryOptions = clientContext.ClientOptions.GetConnectionPolicy().RetryOptions;
            this.batchAsyncContainerExecutor = batchAsyncContainerExecutor;
            this.retryPolicyInstance = new ResourceThrottleRetryPolicy(
                retryOptions.MaxRetryAttemptsOnThrottledRequests,
                retryOptions.MaxRetryWaitTimeInSeconds);
        }

        public async Task<ResponseMessage> SendAsync(
            ItemBatchOperation itemBatchOperation,
            ItemRequestOptions itemRequestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await AbstractRetryHandler.ExecuteHttpRequestAsync(
                    callbackMethod: async () =>
                    {
                        BatchOperationResult operationResult = await this.batchAsyncContainerExecutor.AddAsync(itemBatchOperation, itemRequestOptions);
                        return operationResult.ToResponseMessage();
                    },
                    callShouldRetry: (cosmosResponseMessage, token) =>
                    {
                        return this.retryPolicyInstance.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
                    },
                    callShouldRetryException: (exception, token) =>
                    {
                        return this.retryPolicyInstance.ShouldRetryAsync(exception, cancellationToken);
                    },
                    cancellationToken: cancellationToken);
        }
    }
}
