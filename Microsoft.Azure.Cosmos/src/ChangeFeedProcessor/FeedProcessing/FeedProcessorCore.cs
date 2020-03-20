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

    internal sealed class FeedProcessorCore<T> : FeedProcessor
    {
        private readonly ProcessorOptions options;
        private readonly PartitionCheckpointer checkpointer;
        private readonly ChangeFeedObserver<T> observer;
        private readonly ChangeFeedIterator resultSetIterator;
        private readonly CosmosSerializerCore serializerCore;

        public FeedProcessorCore(
            ChangeFeedObserver<T> observer,
            ChangeFeedIterator resultSetIterator,
            ProcessorOptions options,
            PartitionCheckpointer checkpointer,
            CosmosSerializerCore serializerCore)
        {
            this.observer = observer;
            this.options = options;
            this.checkpointer = checkpointer;
            this.resultSetIterator = resultSetIterator;
            this.serializerCore = serializerCore;
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
                    throw new FeedSplitException("Partition split.", lastContinuation);
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
            ChangeFeedObserverContext context = new ChangeFeedObserverContextCore<T>(this.options.LeaseToken, response, this.checkpointer);
            IEnumerable<T> asFeedResponse;
            try
            {
                asFeedResponse = this.serializerCore.FromFeedResponseStream<T>(
                    response.Content,
                    Documents.ResourceType.Document);
            }
            catch (Exception serializationException)
            {
                // Error using custom serializer to parse stream
                throw new ObserverException(serializationException);
            }

            // When StartFromBeginning is used, the first request returns OK but no content
            if (!asFeedResponse.Any())
            {
                return Task.CompletedTask;
            }

            List<T> asReadOnlyList = new List<T>(asFeedResponse);

            return this.observer.ProcessChangesAsync(context, asReadOnlyList, cancellationToken);
        }
    }
}