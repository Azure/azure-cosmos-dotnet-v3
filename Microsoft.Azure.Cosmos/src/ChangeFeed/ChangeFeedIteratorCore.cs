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
        private readonly IDocumentContainer documentContainer;
        private readonly AsyncLazy<TryCatch<CrossPartitionChangeFeedAsyncEnumerator>> lazyMonadicEnumerator;
        private bool hasMoreResults;

        public ChangeFeedIteratorCore(
            IDocumentContainer documentContainer,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            ChangeFeedStartFrom changeFeedStartFrom)
        {
            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.lazyMonadicEnumerator = new AsyncLazy<TryCatch<CrossPartitionChangeFeedAsyncEnumerator>>(
                valueFactory: async (cancellationToken) =>
                {
                    if (changeFeedStartFrom is ChangeFeedStartFromContinuation startFromContinuation)
                    {
                        TryCatch<CosmosElement> monadicParsedToken = CosmosElement.Monadic.Parse(startFromContinuation.Continuation);
                        if (monadicParsedToken.Failed)
                        {
                            return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                new MalformedChangeFeedContinuationTokenException(
                                    message: $"Failed to parse continuation token: {startFromContinuation.Continuation}.",
                                    innerException: monadicParsedToken.Exception));
                        }

                        TryCatch<VersionedAndRidCheckedCompositeToken> monadicVersionedToken = VersionedAndRidCheckedCompositeToken
                            .MonadicCreateFromCosmosElement(monadicParsedToken.Result);
                        if (monadicVersionedToken.Failed)
                        {
                            return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                new MalformedChangeFeedContinuationTokenException(
                                    message: $"Failed to parse continuation token: {startFromContinuation.Continuation}.",
                                    innerException: monadicVersionedToken.Exception));
                        }

                        VersionedAndRidCheckedCompositeToken versionedAndRidCheckedCompositeToken = monadicVersionedToken.Result;
                        if (versionedAndRidCheckedCompositeToken.VersionNumber != VersionedAndRidCheckedCompositeToken.Version.V2)
                        {
                            return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                new MalformedChangeFeedContinuationTokenException(
                                    message: $"Wrong version number: {versionedAndRidCheckedCompositeToken.VersionNumber}."));
                        }

                        string collectionRid = await documentContainer.GetResourceIdentifierAsync(cancellationToken);
                        if (versionedAndRidCheckedCompositeToken.Rid != collectionRid)
                        {
                            return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                new MalformedChangeFeedContinuationTokenException(
                                    message: $"rids mismatched. Expected: {collectionRid} but got {versionedAndRidCheckedCompositeToken.Rid}."));
                        }

                        changeFeedStartFrom = ChangeFeedStartFrom.ContinuationToken(versionedAndRidCheckedCompositeToken.ContinuationToken.ToString());
                    }

                    TryCatch<CrossPartitionChangeFeedAsyncEnumerator> monadicEnumerator = CrossPartitionChangeFeedAsyncEnumerator.MonadicCreate(
                        documentContainer,
                        changeFeedRequestOptions,
                        changeFeedStartFrom,
                        cancellationToken: default);

                    return monadicEnumerator;
                });
            this.hasMoreResults = true;

        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<CrossPartitionChangeFeedAsyncEnumerator> monadicEnumerator = await this.lazyMonadicEnumerator.GetValueAsync(cancellationToken);

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

            CosmosElement innerContinuationToken = ((ChangeFeedStateContinuation)changeFeedPage.State).ContinuationToken;
            VersionedAndRidCheckedCompositeToken versionedAndRidCheckedCompositeToken = new VersionedAndRidCheckedCompositeToken(
                VersionedAndRidCheckedCompositeToken.Version.V2,
                innerContinuationToken,
                await this.documentContainer.GetResourceIdentifierAsync(cancellationToken));

            responseMessage.Headers.ContinuationToken = VersionedAndRidCheckedCompositeToken.ToCosmosElement(versionedAndRidCheckedCompositeToken).ToString();
            responseMessage.Headers.RequestCharge = changeFeedPage.RequestCharge;
            responseMessage.Headers.ActivityId = changeFeedPage.ActivityId;

            return responseMessage;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotSupportedException();
        }
    }
}