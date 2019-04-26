//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using System.Collections.ObjectModel;
    using System.Net;

    internal sealed class FeedProcessorCore<T> : FeedProcessor
    {
        private readonly ILog logger = LogProvider.GetCurrentClassLogger();
        private readonly ProcessorSettings settings;
        private readonly PartitionCheckpointer checkpointer;
        private readonly ChangeFeedObserver<T> observer;
        private readonly CosmosFeedResultSetIterator resultSetIterator;
        private readonly CosmosJsonSerializer cosmosJsonSerializer;

        public FeedProcessorCore(
            ChangeFeedObserver<T> observer,
            CosmosFeedResultSetIterator resultSetIterator, 
            ProcessorSettings settings, 
            PartitionCheckpointer checkpointer, 
            CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.observer = observer;
            this.settings = settings;
            this.checkpointer = checkpointer;
            this.resultSetIterator = resultSetIterator;
            this.cosmosJsonSerializer = cosmosJsonSerializer;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            string lastContinuation = this.settings.StartContinuation;

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay = this.settings.FeedPollDelay;

                try
                {
                    do
                    {
                        CosmosResponseMessage response;
                        try
                        {
                            response = await this.resultSetIterator.FetchNextSetAsync(cancellationToken).ConfigureAwait(false);
                            response.EnsureSuccessStatusCodeOrNotModified();
                        }
                        catch (CosmosException cosmosException)
                        {
                            this.logger.WarnFormat("unsuccessful feed read: lease token '{0}' status code {1}. substatuscode {2}", this.settings.LeaseToken, cosmosException.StatusCode, cosmosException.SubStatusCode);
                            this.HandleFailedRequest(cosmosException.StatusCode, cosmosException.SubStatusCode, lastContinuation);

                            if (cosmosException.TryGetHeader(Documents.HttpConstants.HttpHeaders.RetryAfterInMilliseconds, out string retryAfterString))
                            {
                                TimeSpan? retryAfter = CosmosResponseMessageHeaders.GetRetryAfter(retryAfterString);
                                if (retryAfter.HasValue)
                                {
                                    delay = retryAfter.Value;
                                }
                            }

                            // Out of the loop for a retry
                            break;
                        }

                        lastContinuation = response.Headers.Continuation;
                        if (this.resultSetIterator.HasMoreResults)
                        {
                            await this.DispatchChanges(response, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    while (this.resultSetIterator.HasMoreResults && !cancellationToken.IsCancellationRequested);
                    }
                }
                catch (TaskCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    this.logger.WarnException("exception: lease token '{0}'", canceledException, this.settings.LeaseToken);

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
                    this.logger.Fatal($"Unrecognized DocDbError enum value {docDbError}");
                    Debug.Fail($"Unrecognized DocDbError enum value {docDbError}");
                    throw new InvalidOperationException($"Unrecognized DocDbError enum value {docDbError} for status code {statusCode} and substatus code {subStatusCode}");
            }
        }

        private Task DispatchChanges(CosmosResponseMessage response, CancellationToken cancellationToken)
        {
            ChangeFeedObserverContext context = new ChangeFeedObserverContextCore<T>(this.settings.LeaseToken, response, this.checkpointer);
            Collection<T> asFeedResponse;
            try
            {
                asFeedResponse = cosmosJsonSerializer.FromStream<CosmosFeedResponse<T>>(response.Content).Data;
            }
            catch (Exception serializationException)
            {
                // Error using custom serializer to parse stream
                throw new ObserverException(serializationException);
            }

            List<T> asReadOnlyList = new List<T>(asFeedResponse.Count);
            asReadOnlyList.AddRange(asFeedResponse);

            return this.observer.ProcessChangesAsync(context, asReadOnlyList, cancellationToken);
        }
    }
}