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
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;

    internal sealed class ChangeFeedIteratorCore : FeedIteratorInternal
    {
        private readonly AsyncLazy<TryCatch<CrossPartitionChangeFeedAsyncEnumerator>> asyncLazyEnumerator;
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

            if (changeFeedRequestOptions == null)
            {
                throw new ArgumentNullException(nameof(changeFeedRequestOptions));
            }

            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            this.asyncLazyEnumerator = new AsyncLazy<TryCatch<CrossPartitionChangeFeedAsyncEnumerator>>(
                valueFactory: async (CancellationToken cancellationToken) => await CrossPartitionChangeFeedAsyncEnumerator.MonadicCreateAsync(
                    documentContainer,
                    changeFeedRequestOptions,
                    changeFeedStartFrom,
                    cancellationToken));
            this.hasMoreResults = true;
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<CrossPartitionChangeFeedAsyncEnumerator> monadicEnumerator = await this.asyncLazyEnumerator.GetValueAsync(cancellationToken);
            if (monadicEnumerator.Failed)
            {
                Exception createException = monadicEnumerator.Exception;
                CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(createException);
                return new ResponseMessage(
                    cosmosException.StatusCode,
                    requestMessage: null,
                    headers: cosmosException.Headers,
                    cosmosException: cosmosException,
                    diagnostics: new CosmosDiagnosticsContextCore());
            }

            CrossPartitionChangeFeedAsyncEnumerator enumerator = monadicEnumerator.Result;

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
            ResponseMessage responseMessage = new ResponseMessage(
                statusCode: changeFeedPage.ContentWasModified ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotModified)
            {
                Content = changeFeedPage.Content
            };
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