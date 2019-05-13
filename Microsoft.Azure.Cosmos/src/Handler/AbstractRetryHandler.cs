//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    internal abstract class AbstractRetryHandler : CosmosRequestHandler
    {
        internal abstract Task<IDocumentClientRetryPolicy> GetRetryPolicy(CosmosRequestMessage request);

        public override async Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellation)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = await this.GetRetryPolicy(request);

            try
            {
                return await RetryHandler.ExecuteHttpRequestAsync(
                    callbackMethod: () =>
                    {
                        return base.SendAsync(request, cancellation);
                    },
                    callShouldRetry: (cosmosResponseMessage, token) =>
                    {
                        return retryPolicyInstance.ShouldRetryAsync(cosmosResponseMessage, cancellation);
                    },
                    callShouldRetryException: (exception, token) =>
                    {
                        return retryPolicyInstance.ShouldRetryAsync(exception, cancellation);
                    },
                    cancellation: cancellation);
            }
            catch (DocumentClientException ex)
            {
                return ex.ToCosmosResponseMessage(request);
            }
            catch (AggregateException ex)
            {
                // TODO: because the SDK underneath this path uses ContinueWith or task.Result we need to catch AggregateExceptions here
                // in order to ensure that underlying DocumentClientExceptions get propagated up correctly. Once all ContinueWith and .Result 
                // is removed this catch can be safely removed.
                AggregateException innerExceptions = ex.Flatten();
                Exception docClientException = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is DocumentClientException);
                if (docClientException != null)
                {
                    return ((DocumentClientException)docClientException).ToCosmosResponseMessage(request);
                }

                throw;
            }
        }

        private static async Task<CosmosResponseMessage> ExecuteHttpRequestAsync(
           Func<Task<CosmosResponseMessage>> callbackMethod,
           Func<CosmosResponseMessage, CancellationToken, Task<ShouldRetryResult>> callShouldRetry,
           Func<Exception, CancellationToken, Task<ShouldRetryResult>> callShouldRetryException,
           CancellationToken cancellation = default(CancellationToken))
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                ShouldRetryResult result;

                try
                {
                    CosmosResponseMessage cosmosResponseMessage = await callbackMethod();
                    if (cosmosResponseMessage.IsSuccessStatusCode)
                    {
                        return cosmosResponseMessage;
                    }

                    result = await callShouldRetry(cosmosResponseMessage, cancellation);
                    if (!result.ShouldRetry)
                    {
                        return cosmosResponseMessage;
                    }
                }
                catch (HttpRequestException httpRequestException)
                {
                    result = await callShouldRetryException(httpRequestException, cancellation);
                    if (!result.ShouldRetry)
                    {
                        // Today we don't translate request exceptions into status codes since this was an error before
                        // making the request. TODO: Figure out how to pipe this as a response instead of throwing?
                        throw;
                    }
                }

                TimeSpan backoffTime = result.BackoffTime;
                if (backoffTime != TimeSpan.Zero)
                {
                    await Task.Delay(result.BackoffTime, cancellation);
                }
            }
        }
    }
} 