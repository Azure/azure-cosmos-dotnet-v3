//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal sealed class ReadFeedIteratorCore : FeedIteratorInternal
    {
        private readonly TryCatch<CrossPartitionReadFeedAsyncEnumerator> monadicEnumerator;
        private readonly CosmosDiagnosticsContext diagnosticsContext;
        private bool hasMoreResults;

        public ReadFeedIteratorCore(
            IDocumentContainer documentContainer,
            QueryRequestOptions queryRequestOptions,
            string continuationToken,
            int pageSize,
            CancellationToken cancellationToken,
            CosmosDiagnosticsContext diagnosticsContext = null)
        {
            if (!string.IsNullOrEmpty(continuationToken))
            {
                bool isNewArrayFormat = (continuationToken.Length >= 2) && (continuationToken[0] == '[') && (continuationToken[continuationToken.Length - 1] == ']');
                if (!isNewArrayFormat)
                {
                    // One of the two older formats
                    if (!FeedRangeContinuation.TryParse(continuationToken, out FeedRangeContinuation feedRangeContinuation))
                    {
                        // Backward compatible with old format
                        feedRangeContinuation = new FeedRangeCompositeContinuation(
                            containerRid: string.Empty,
                            FeedRangeEpk.FullRange,
                            new List<Documents.Routing.Range<string>>()
                            {
                            new Documents.Routing.Range<string>(
                                Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                                Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                                isMinInclusive: true,
                                isMaxInclusive: false)
                            },
                            continuationToken);
                    }

                    // need to massage it a little
                    string oldContinuationFormat = feedRangeContinuation.ToString();
                    CosmosObject cosmosObject = CosmosObject.Parse(oldContinuationFormat);
                    CosmosArray continuations = (CosmosArray)cosmosObject["Continuation"];

                    List<CosmosElement> readFeedContinuationTokens = new List<CosmosElement>();
                    foreach (CosmosElement continuation in continuations)
                    {
                        CosmosObject continuationObject = (CosmosObject)continuation;
                        string min = ((CosmosString)continuationObject["min"]).Value;
                        string max = ((CosmosString)continuationObject["max"]).Value;
                        CosmosElement token = continuationObject["token"];

                        FeedRangeInternal feedRange = new FeedRangeEpk(new Documents.Routing.Range<string>(min, max, isMinInclusive: true, isMaxInclusive: false));
                        ReadFeedState state = new ReadFeedState(token);
                        ReadFeedContinuationToken readFeedContinuationToken = new ReadFeedContinuationToken(feedRange, state);
                        readFeedContinuationTokens.Add(ReadFeedContinuationToken.ToCosmosElement(readFeedContinuationToken));
                    }

                    CosmosArray cosmosArrayContinuationTokens = CosmosArray.Create(readFeedContinuationTokens);
                    continuationToken = cosmosArrayContinuationTokens.ToString();
                }
            }

            this.monadicEnumerator = CrossPartitionReadFeedAsyncEnumerator.MonadicCreate(
                documentContainer,
                queryRequestOptions,
                continuationToken: continuationToken,
                pageSize,
                cancellationToken);

            this.diagnosticsContext = diagnosticsContext;

            this.hasMoreResults = true;
        }

        public override bool HasMoreResults => this.hasMoreResults;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (this.diagnosticsContext.GetOverallScope())
            {
                if (!this.hasMoreResults)
                {
                    throw new InvalidOperationException("Should not be calling FeedIterator that does not have any more results");
                }

                if (this.monadicEnumerator.Failed)
                {
                    this.hasMoreResults = false;

                    CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(this.monadicEnumerator.Exception);
                    return new ResponseMessage(
                        statusCode: System.Net.HttpStatusCode.BadRequest,
                        requestMessage: null,
                        headers: cosmosException.Headers,
                        cosmosException: cosmosException,
                        diagnostics: this.diagnosticsContext);
                }

                CrossPartitionReadFeedAsyncEnumerator enumerator = this.monadicEnumerator.Result;
                TryCatch<ReadFeedPage> monadicPage;

                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        throw new InvalidOperationException("Should not be calling enumerator that does not have any more results");
                    }

                    monadicPage = enumerator.Current;
                }
                catch (OperationCanceledException ex) when (!(ex is CosmosOperationCanceledException))
                {
                    throw new CosmosOperationCanceledException(ex, this.diagnosticsContext);
                }
                catch (Exception ex)
                {
                    monadicPage = TryCatch<ReadFeedPage>.FromException(ex);
                }

                if (monadicPage.Failed)
                {
                    CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(monadicPage.Exception);
                    if (!IsRetriableException(cosmosException))
                    {
                        this.hasMoreResults = false;
                    }

                    return new ResponseMessage(
                        statusCode: cosmosException.StatusCode,
                        requestMessage: null,
                        headers: cosmosException.Headers,
                        cosmosException: cosmosException,
                        diagnostics: this.diagnosticsContext);
                }

                ReadFeedPage readFeedPage = monadicPage.Result;
                if (readFeedPage.State == default)
                {
                    this.hasMoreResults = false;
                }

                return new ResponseMessage(
                    statusCode: System.Net.HttpStatusCode.OK,
                    requestMessage: default,
                    headers: new Headers()
                    {
                        RequestCharge = readFeedPage.RequestCharge,
                        ActivityId = readFeedPage.ActivityId,
                        ContinuationToken = readFeedPage.State?.ContinuationToken.ToString(),
                    },
                    cosmosException: default,
                    diagnostics: this.diagnosticsContext)
                {
                    Content = readFeedPage.Content,
                };
            }
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotSupportedException();
        }
    }
}
