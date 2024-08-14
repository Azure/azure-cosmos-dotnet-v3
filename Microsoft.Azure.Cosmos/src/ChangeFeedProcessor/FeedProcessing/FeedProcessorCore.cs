//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;

    internal sealed class FeedProcessorCore : FeedProcessor
    {
        private readonly ProcessorOptions options;
        private readonly PartitionCheckpointer checkpointer;
        private readonly ChangeFeedObserver observer;
        private readonly FeedIterator resultSetIterator;

        public FeedProcessorCore(
            ChangeFeedObserver observer,
            FeedIterator resultSetIterator,
            ProcessorOptions options,
            PartitionCheckpointer checkpointer)
        {
            this.observer = observer ?? throw new ArgumentNullException(nameof(observer));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.checkpointer = checkpointer ?? throw new ArgumentNullException(nameof(checkpointer));
            this.resultSetIterator = resultSetIterator ?? throw new ArgumentNullException(nameof(resultSetIterator));
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            string lastContinuation = this.options.StartContinuation;

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay = this.options.FeedPollDelay;

                try
                {
                    do
                    {
                        Task<ResponseMessage> task = this.resultSetIterator.ReadNextAsync(cancellationToken);
                        
                        using (CancellationTokenSource cts = new CancellationTokenSource())
                        {
                            if (!ReferenceEquals(await Task.WhenAny(task, Task.Delay(this.options.RequestTimeout, cts.Token)), task))
                            {
                                Task catchExceptionFromTask = task.ContinueWith(task => DefaultTrace.TraceInformation(
                                "Timed out Change Feed request failed with exception: {2}", task.Exception.InnerException),
                                TaskContinuationOptions.OnlyOnFaulted);
                                throw CosmosExceptionFactory.CreateRequestTimeoutException("Change Feed request timed out", new Headers());
                            }
                            else
                            {
                                cts.Cancel();
                            }
                        }

                        ResponseMessage response = await task;

                        if (response.StatusCode != HttpStatusCode.NotModified && !response.IsSuccessStatusCode)
                        {
                            DefaultTrace.TraceWarning("unsuccessful feed read: lease token '{0}' status code {1}. substatuscode {2}", this.options.LeaseToken, response.StatusCode, response.Headers.SubStatusCode);
                            this.HandleFailedRequest(response, lastContinuation);

                            if (response.Headers.RetryAfter.HasValue)
                            {
                                delay = response.Headers.RetryAfter.Value;
                            }

                            // Out of the loop for a retry
                            break;
                        }

                        lastContinuation = response.Headers.ContinuationToken;
                        if (this.resultSetIterator.HasMoreResults)
                        {
                            await this.DispatchChangesAsync(response, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    while (this.resultSetIterator.HasMoreResults && !cancellationToken.IsCancellationRequested);
                }
                catch (OperationCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    Extensions.TraceException(canceledException);
                    DefaultTrace.TraceWarning("exception: lease token '{0}'", this.options.LeaseToken);

                    // ignore as it is caused by Cosmos DB client when StopAsync is called
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        private void HandleFailedRequest(
            ResponseMessage responseMessage,
            string lastContinuation)
        {
            DocDbError docDbError = ExceptionClassifier.ClassifyStatusCodes(responseMessage.StatusCode, (int)responseMessage.Headers.SubStatusCode);
            switch (docDbError)
            {
                case DocDbError.PartitionSplit:
                    throw new FeedRangeGoneException("Partition split.", lastContinuation);
                case DocDbError.Undefined:
                    throw CosmosExceptionFactory.Create(responseMessage);
                default:
                    DefaultTrace.TraceCritical($"Unrecognized DocDbError enum value {docDbError}");
                    Debug.Fail($"Unrecognized DocDbError enum value {docDbError}");
                    throw new InvalidOperationException($"Unrecognized DocDbError enum value {docDbError} for status code {responseMessage.StatusCode} and substatus code {responseMessage.Headers.SubStatusCode}");
            }
        }

        private Task DispatchChangesAsync(ResponseMessage response, CancellationToken cancellationToken)
        {
            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(
                this.options.LeaseToken,
                response,
                this.checkpointer,
                this.options.FeedRange);
            return this.observer.ProcessChangesAsync(context, response.Content, cancellationToken);
        }
    }
}