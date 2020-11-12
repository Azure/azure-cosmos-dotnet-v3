//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
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
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal sealed class ReadFeedIteratorCore : FeedIteratorInternal
    {
        private readonly TryCatch<CrossPartitionReadFeedAsyncEnumerator> monadicEnumerator;
        private bool hasMoreResults;

        public ReadFeedIteratorCore(
            IDocumentContainer documentContainer,
            QueryRequestOptions queryRequestOptions,
            string continuationToken,
            int pageSize,
            CancellationToken cancellationToken)
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
                        CosmosObject rangeObject = (CosmosObject)continuationObject["range"];
                        string min = ((CosmosString)rangeObject["min"]).Value;
                        string max = ((CosmosString)rangeObject["max"]).Value;
                        CosmosElement token = continuationObject["token"];

                        FeedRangeInternal feedRange = new FeedRangeEpk(new Documents.Routing.Range<string>(min, max, isMinInclusive: true, isMaxInclusive: false));
                        ReadFeedState state;
                        if (token is CosmosNull)
                        {
                            state = ReadFeedState.Beginning();
                        }
                        else
                        {
                            CosmosString tokenAsString = (CosmosString)token;
                            state = ReadFeedState.Continuation(CosmosElement.Parse(tokenAsString.Value));
                        }

                        FeedRangeState<ReadFeedState> readFeedContinuationToken = new FeedRangeState<ReadFeedState>(feedRange, state);
                        readFeedContinuationTokens.Add(ReadFeedFeedRangeStateSerializer.ToCosmosElement(readFeedContinuationToken));
                    }

                    CosmosArray cosmosArrayContinuationTokens = CosmosArray.Create(readFeedContinuationTokens);
                    continuationToken = cosmosArrayContinuationTokens.ToString();
                }
            }

            TryCatch<ReadFeedCrossFeedRangeState> monadicReadFeedState;
            if (continuationToken == null)
            {
                monadicReadFeedState = TryCatch<ReadFeedCrossFeedRangeState>.FromResult(ReadFeedCrossFeedRangeState.CreateFromBeginning());
            }
            else
            {
                monadicReadFeedState = ReadFeedCrossFeedRangeState.Monadic.Parse(continuationToken);
            }

            if (monadicReadFeedState.Failed)
            {
                this.monadicEnumerator = TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromException(monadicReadFeedState.Exception);
            }
            else
            {
                this.monadicEnumerator = TryCatch<CrossPartitionReadFeedAsyncEnumerator>.FromResult(
                    CrossPartitionReadFeedAsyncEnumerator.Create(
                        documentContainer,
                        queryRequestOptions,
                        new CrossFeedRangeState<ReadFeedState>(monadicReadFeedState.Result.FeedRangeStates),
                        pageSize,
                        cancellationToken));
            }

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
                    diagnostics: cosmosException.DiagnosticsContext);
            }

            CrossPartitionReadFeedAsyncEnumerator enumerator = this.monadicEnumerator.Result;
            TryCatch<CrossFeedRangePage<Pagination.ReadFeedPage, ReadFeedState>> monadicPage;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    throw new InvalidOperationException("Should not be calling enumerator that does not have any more results");
                }

                monadicPage = enumerator.Current;
            }
            catch (Exception ex)
            {
                monadicPage = TryCatch<CrossFeedRangePage<Pagination.ReadFeedPage, ReadFeedState>>.FromException(ex);
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
                    diagnostics: cosmosException.DiagnosticsContext);
            }

            CrossFeedRangePage<Pagination.ReadFeedPage, ReadFeedState> crossFeedRangePage = monadicPage.Result;
            if (crossFeedRangePage.State == default)
            {
                this.hasMoreResults = false;
            }

            // Make the continuation token match the older format:
            string continuationToken;
            if (crossFeedRangePage.State != null)
            {
                List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>();
                CrossFeedRangeState<ReadFeedState> crossFeedRangeState = crossFeedRangePage.State;
                for (int i = 0; i < crossFeedRangeState.Value.Length; i++)
                {
                    FeedRangeState<ReadFeedState> feedRangeState = crossFeedRangeState.Value.Span[i];
                    FeedRangeEpk feedRangeEpk = (FeedRangeEpk)feedRangeState.FeedRange;
                    ReadFeedState readFeedState = feedRangeState.State;
                    CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
                    {
                        Range = feedRangeEpk.Range,
                        Token = readFeedState is ReadFeedBeginningState ? null : ((ReadFeedContinuationState)readFeedState).ContinuationToken.ToString(),
                    };

                    compositeContinuationTokens.Add(compositeContinuationToken);
                }

                FeedRangeCompositeContinuation feedRangeCompositeContinuation = new FeedRangeCompositeContinuation(
                    containerRid: string.Empty,
                    feedRange: FeedRangeEpk.FullRange,
                    compositeContinuationTokens);

                continuationToken = feedRangeCompositeContinuation.ToString();
            }
            else
            {
                continuationToken = null;
            }

            Pagination.ReadFeedPage page = crossFeedRangePage.Page;
            return new ResponseMessage(
                statusCode: System.Net.HttpStatusCode.OK,
                requestMessage: default,
                headers: new Headers()
                {
                    RequestCharge = page.RequestCharge,
                    ActivityId = page.ActivityId,
                    ContinuationToken = continuationToken,
                },
                cosmosException: default,
                diagnostics: page.Diagnostics)
            {
                Content = page.Content,
            };
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotSupportedException();
        }
    }
}
