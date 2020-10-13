//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    /// <remarks>
    /// Legacy, see <see cref="ChangeFeedIteratorCore"/>.
    /// </remarks>
    /// <seealso cref="ChangeFeedIteratorCore"/>
    internal class StandByFeedIteratorCore : FeedIteratorInternal
    {
        private readonly ChangeFeedIteratorCore changeFeedIteratorCore;

        internal StandByFeedIteratorCore(
            IDocumentContainer documentContainer,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            this.changeFeedIteratorCore = new ChangeFeedIteratorCore(documentContainer, changeFeedRequestOptions, changeFeedStartFrom);
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly ChangeFeedRequestOptions changeFeedOptions;

        public override bool HasMoreResults => true;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.changeFeedIteratorCore.ReadNextAsync(cancellationToken);
            if (responseMessage.ContinuationToken != null)
            {
                List<CompositeContinuationToken> compositeContinuationTokensBuilder = new List<CompositeContinuationToken>();

                // need to modify continuation token for back compat
                CosmosArray compositeContinuationTokens = CosmosArray.Parse(responseMessage.ContinuationToken);
                foreach (CosmosElement arrayItem in compositeContinuationTokens)
                {
                    CosmosObject compositeContinuationToken = (CosmosObject)arrayItem;
                    FeedRangeInternal feedRange = FeedRangeCosmosElementSerializer.MonadicCreateFromCosmosElement(
                        compositeContinuationToken["FeedRange"])
                        .Result;
                    ChangeFeedState changeFeedState = ChangeFeedStateCosmosElementSerializer.MonadicFromCosmosElement(
                        compositeContinuationToken["State"])
                        .Result;

                    if (!(feedRange is FeedRangeEpk feedRangeEpk))
                    {
                        throw new InvalidOperationException();
                    }

                    Microsoft.Azure.Documents.Range<string> range = new Documents.Range<string>()

                    if (!(changeFeedState is ChangeFeedStartFromContinuation changeFeedStateFromContinuation))
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        internal virtual Task<ResponseMessage> NextResultSetDelegateAsync(
            ChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}