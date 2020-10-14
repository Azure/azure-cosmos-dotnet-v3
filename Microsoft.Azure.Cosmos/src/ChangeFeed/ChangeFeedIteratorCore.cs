//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class ChangeFeedIteratorCore : FeedIteratorInternal
    {
        private readonly TryCatch<CrossPartitionChangeFeedAsyncEnumerator> monadicEnumerator;
        private bool hasMoreResults;

        public ChangeFeedIteratorCore(
            IDocumentContainer documentContainer,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            ChangeFeedStartFrom changeFeedStartFrom)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            this.monadicEnumerator = CrossPartitionChangeFeedAsyncEnumerator.MonadicCreate(
                documentContainer,
                changeFeedRequestOptions,
                changeFeedStartFrom,
                cancellationToken: default);
            this.hasMoreResults = true;
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.monadicEnumerator.Failed)
            {
                Exception createException = this.monadicEnumerator.Exception;
                CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(createException);
                return new ResponseMessage(
                    cosmosException.StatusCode,
                    requestMessage: null,
                    headers: cosmosException.Headers,
                    cosmosException: cosmosException,
                    diagnostics: new CosmosDiagnosticsContextCore());
            }

            CrossPartitionChangeFeedAsyncEnumerator enumerator = this.monadicEnumerator.Result;

            if (!await enumerator.MoveNextAsync())
            {
                throw new InvalidOperationException("ChangeFeed enumerator should always have a next continuation");
            }

            if (enumerator.Current.Failed)
            {
                CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(enumerator.Current.Exception);
                if (!IsRetriableException(cosmosException))
                {
                    this.hasMoreResults = false;
                }

                return new ResponseMessage(
                    cosmosException.StatusCode,
                    requestMessage: null,
                    headers: cosmosException.Headers,
                    cosmosException: cosmosException,
                    diagnostics: new CosmosDiagnosticsContextCore());
            }

            ChangeFeedPage changeFeedPage = enumerator.Current.Result;
            ResponseMessage responseMessage;
            if (changeFeedPage is ChangeFeedSuccessPage changeFeedSuccessPage)
            {
                responseMessage = new ResponseMessage(statusCode: System.Net.HttpStatusCode.OK)
                {
                    Content = changeFeedSuccessPage.Content
                };
            }
            else
            {
                responseMessage = new ResponseMessage(statusCode: System.Net.HttpStatusCode.NotModified);
            }

            responseMessage.Headers.ContinuationToken = ((ChangeFeedStateContinuation)changeFeedPage.State).ContinuationToken.ToString();
            responseMessage.Headers.RequestCharge = changeFeedPage.RequestCharge;
            responseMessage.Headers.ActivityId = changeFeedPage.ActivityId;

            return responseMessage;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}