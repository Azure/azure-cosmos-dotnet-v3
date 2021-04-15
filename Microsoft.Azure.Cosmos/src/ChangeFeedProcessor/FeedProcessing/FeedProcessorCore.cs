//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;

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
                        ResponseMessage response = await this.resultSetIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                        if (response.StatusCode != HttpStatusCode.NotModified && !response.IsSuccessStatusCode)
                        {
                            DefaultTrace.TraceWarning("unsuccessful feed read: lease token '{0}' status code {1}. substatuscode {2}", this.options.LeaseToken, response.StatusCode, response.Headers.SubStatusCode);
                            this.HandleFailedRequest(response.StatusCode, (int)response.Headers.SubStatusCode, lastContinuation);

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
                catch (TaskCanceledException canceledException)
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
            HttpStatusCode statusCode,
            int subStatusCode,
            string lastContinuation)
        {
            DocDbError docDbError = ExceptionClassifier.ClassifyStatusCodes(statusCode, subStatusCode);
            switch (docDbError)
            {
                case DocDbError.PartitionSplit:
                    throw new FeedRangeGoneException("Partition split.", lastContinuation);
                case DocDbError.PartitionNotFound:
                    throw new FeedNotFoundException("Partition not found.", lastContinuation);
                case DocDbError.ReadSessionNotAvailable:
                    throw new FeedReadSessionNotAvailableException("Read session not availalbe.", lastContinuation);
                case DocDbError.Undefined:
                    throw new InvalidOperationException($"Undefined DocDbError for status code {statusCode} and substatus code {subStatusCode}");
                default:
                    DefaultTrace.TraceCritical($"Unrecognized DocDbError enum value {docDbError}");
                    Debug.Fail($"Unrecognized DocDbError enum value {docDbError}");
                    throw new InvalidOperationException($"Unrecognized DocDbError enum value {docDbError} for status code {statusCode} and substatus code {subStatusCode}");
            }
        }

        private Task DispatchChangesAsync(ResponseMessage response, CancellationToken cancellationToken)
        {
            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(this.options.LeaseToken, response, this.checkpointer);
            return this.observer.ProcessChangesAsync(context, response.Content, cancellationToken);
        }
    }
}