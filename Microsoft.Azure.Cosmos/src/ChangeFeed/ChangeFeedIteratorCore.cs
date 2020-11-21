//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;

    internal sealed class ChangeFeedIteratorCore : FeedIteratorInternal
    {
        private readonly IDocumentContainer documentContainer;
        private readonly ChangeFeedRequestOptions changeFeedRequestOptions;
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
            this.changeFeedRequestOptions = changeFeedRequestOptions ?? new ChangeFeedRequestOptions();
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
                        if (versionedAndRidCheckedCompositeToken.VersionNumber == VersionedAndRidCheckedCompositeToken.Version.V1)
                        {
                            // Need to migrate continuation token
                            if (!(versionedAndRidCheckedCompositeToken.ContinuationToken is CosmosArray cosmosArray))
                            {
                                return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                    new MalformedChangeFeedContinuationTokenException(
                                        message: $"Failed to parse get array continuation token: {startFromContinuation.Continuation}."));
                            }

                            List<CosmosElement> changeFeedTokensV2 = new List<CosmosElement>();
                            foreach (CosmosElement arrayItem in cosmosArray)
                            {
                                if (!(arrayItem is CosmosObject cosmosObject))
                                {
                                    return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                        new MalformedChangeFeedContinuationTokenException(
                                            message: $"Failed to parse get object in composite continuation: {startFromContinuation.Continuation}."));
                                }

                                if (!cosmosObject.TryGetValue("min", out CosmosString min))
                                {
                                    return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                        new MalformedChangeFeedContinuationTokenException(
                                            message: $"Failed to parse start of range: {cosmosObject}."));
                                }

                                if (!cosmosObject.TryGetValue("max", out CosmosString max))
                                {
                                    return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                        new MalformedChangeFeedContinuationTokenException(
                                            message: $"Failed to parse end of range: {cosmosObject}."));
                                }

                                if (!cosmosObject.TryGetValue("token", out CosmosElement token))
                                {
                                    return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                                        new MalformedChangeFeedContinuationTokenException(
                                            message: $"Failed to parse token: {cosmosObject}."));
                                }

                                FeedRangeEpk feedRangeEpk = new FeedRangeEpk(new Documents.Routing.Range<string>(
                                    min: min.Value,
                                    max: max.Value,
                                    isMinInclusive: true,
                                    isMaxInclusive: false));
                                ChangeFeedState state = token is CosmosNull ? ChangeFeedState.Beginning() : ChangeFeedStateContinuation.Continuation(token);

                                FeedRangeState<ChangeFeedState> feedRangeState = new FeedRangeState<ChangeFeedState>(feedRangeEpk, state);
                                changeFeedTokensV2.Add(ChangeFeedFeedRangeStateSerializer.ToCosmosElement(feedRangeState));
                            }

                            CosmosArray changeFeedTokensArrayV2 = CosmosArray.Create(changeFeedTokensV2);

                            versionedAndRidCheckedCompositeToken = new VersionedAndRidCheckedCompositeToken(
                                VersionedAndRidCheckedCompositeToken.Version.V2,
                                changeFeedTokensArrayV2,
                                versionedAndRidCheckedCompositeToken.Rid);
                        }

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

                    TryCatch<ChangeFeedCrossFeedRangeState> monadicChangeFeedCrossFeedRangeState = changeFeedStartFrom.Accept(ChangeFeedStateFromToChangeFeedCrossFeedRangeState.Singleton);
                    if (monadicChangeFeedCrossFeedRangeState.Failed)
                    {
                        return TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                message: $"Could not convert to {nameof(ChangeFeedCrossFeedRangeState)}.",
                                innerException: monadicChangeFeedCrossFeedRangeState.Exception));
                    }

                    CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                        documentContainer,
                        changeFeedRequestOptions,
                        new CrossFeedRangeState<ChangeFeedState>(monadicChangeFeedCrossFeedRangeState.Result.FeedRangeStates),
                        cancellationToken: default);

                    TryCatch<CrossPartitionChangeFeedAsyncEnumerator> monadicEnumerator = TryCatch<CrossPartitionChangeFeedAsyncEnumerator>.FromResult(enumerator);
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

            CrossFeedRangePage<Pagination.ChangeFeedPage, ChangeFeedState> crossFeedRangePage = enumerator.Current.Result;
            Pagination.ChangeFeedPage changeFeedPage = crossFeedRangePage.Page;
            ResponseMessage responseMessage;
            if (changeFeedPage is Pagination.ChangeFeedSuccessPage changeFeedSuccessPage)
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

            CrossFeedRangeState<ChangeFeedState> crossFeedRangeState = crossFeedRangePage.State;
            string continuationToken;
            if (this.changeFeedRequestOptions.EmitOldContinuationToken)
            {
                List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>();
                for (int i = 0; i < crossFeedRangeState.Value.Length; i++)
                {
                    FeedRangeState<ChangeFeedState> changeFeedFeedRangeState = crossFeedRangeState.Value.Span[i];
                    string token = changeFeedFeedRangeState.State is ChangeFeedStateContinuation changeFeedStateContinuation ? ((CosmosString)changeFeedStateContinuation.ContinuationToken).Value : null;
                    Documents.Routing.Range<string> range = ((FeedRangeEpk)changeFeedFeedRangeState.FeedRange).Range;
                    CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
                    {
                        Range = range,
                        Token = token,
                    };

                    compositeContinuationTokens.Add(compositeContinuationToken);
                }

                FeedRangeCompositeContinuation feedRangeCompositeContinuationToken = new FeedRangeCompositeContinuation(
                    await this.documentContainer.GetResourceIdentifierAsync(cancellationToken),
                    FeedRangeEpk.FullRange,
                    compositeContinuationTokens);

                continuationToken = feedRangeCompositeContinuationToken.ToString();
            }
            else
            {
                ChangeFeedCrossFeedRangeState changeFeedCrossFeedRangeState = new ChangeFeedCrossFeedRangeState(crossFeedRangeState.Value);
                continuationToken = VersionedAndRidCheckedCompositeToken.ToCosmosElement(
                    new VersionedAndRidCheckedCompositeToken(
                        VersionedAndRidCheckedCompositeToken.Version.V2,
                        changeFeedCrossFeedRangeState.ToCosmosElement(),
                        await this.documentContainer.GetResourceIdentifierAsync(cancellationToken))).ToString();
            }

            responseMessage.Headers.ContinuationToken = continuationToken;
            responseMessage.Headers.RequestCharge = changeFeedPage.RequestCharge;
            responseMessage.Headers.ActivityId = changeFeedPage.ActivityId;

            return responseMessage;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotSupportedException();
        }

        private sealed class ChangeFeedStateFromToChangeFeedCrossFeedRangeState : ChangeFeedStartFromVisitor<TryCatch<ChangeFeedCrossFeedRangeState>>
        {
            public static readonly ChangeFeedStateFromToChangeFeedCrossFeedRangeState Singleton = new ChangeFeedStateFromToChangeFeedCrossFeedRangeState();

            public override TryCatch<ChangeFeedCrossFeedRangeState> Visit(ChangeFeedStartFromNow startFromNow)
            {
                return TryCatch<ChangeFeedCrossFeedRangeState>.FromResult(
                    ChangeFeedCrossFeedRangeState.CreateFromNow(startFromNow.FeedRange));
            }

            public override TryCatch<ChangeFeedCrossFeedRangeState> Visit(ChangeFeedStartFromTime startFromTime)
            {
                return TryCatch<ChangeFeedCrossFeedRangeState>.FromResult(
                     ChangeFeedCrossFeedRangeState.CreateFromTime(startFromTime.StartTime, startFromTime.FeedRange));
            }

            public override TryCatch<ChangeFeedCrossFeedRangeState> Visit(ChangeFeedStartFromContinuation startFromContinuation)
            {
                TryCatch<CosmosElement> monadicParsedToken = CosmosElement.Monadic.Parse(startFromContinuation.Continuation);
                if (monadicParsedToken.Failed)
                {
                    return TryCatch<ChangeFeedCrossFeedRangeState>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            message: $"Failed to parse continuation token: {startFromContinuation.Continuation}.",
                            innerException: monadicParsedToken.Exception));
                }

                return ChangeFeedCrossFeedRangeState.Monadic.CreateFromCosmosElement(monadicParsedToken.Result);
            }

            public override TryCatch<ChangeFeedCrossFeedRangeState> Visit(ChangeFeedStartFromBeginning startFromBeginning)
            {
                return TryCatch<ChangeFeedCrossFeedRangeState>.FromResult(
                     ChangeFeedCrossFeedRangeState.CreateFromBeginning(startFromBeginning.FeedRange));
            }

            public override TryCatch<ChangeFeedCrossFeedRangeState> Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
            {
                throw new NotSupportedException();
            }
        }
    }
}