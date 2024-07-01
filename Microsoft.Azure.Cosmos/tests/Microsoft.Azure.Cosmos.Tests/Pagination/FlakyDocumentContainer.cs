//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Implementation of <see cref="IMonadicDocumentContainer"/> that composes another <see cref="IMonadicDocumentContainer"/> and randomly adds in exceptions.
    /// This is useful for mocking throttles and other edge cases like empty pages.
    /// </summary>
    internal sealed class FlakyDocumentContainer : IMonadicDocumentContainer
    {
        private readonly FailureConfigs failureConfigs;
        private readonly Random random;

        private static class Throttle
        {
            private static readonly CosmosException RequestRateTooLargeException = new CosmosException(
                message: "Request Rate Too Large",
                statusCode: (System.Net.HttpStatusCode)429,
                subStatusCode: default,
                activityId: Guid.NewGuid().ToString(),
                requestCharge: default);

            public static readonly Task<TryCatch<Record>> ForCreateItem = Task.FromResult(
                TryCatch<Record>.FromException(
                    RequestRateTooLargeException));

            public static readonly Task<TryCatch<ReadFeedPage>> ForReadFeed = Task.FromResult(
                TryCatch<ReadFeedPage>.FromException(
                    RequestRateTooLargeException));

            public static readonly Task<TryCatch<QueryPage>> ForQuery = Task.FromResult(
                TryCatch<QueryPage>.FromException(
                    RequestRateTooLargeException));

            public static readonly Task<TryCatch<ChangeFeedPage>> ForChangeFeed = Task.FromResult(
                TryCatch<ChangeFeedPage>.FromException(
                    RequestRateTooLargeException));
        }

        private static readonly QueryState StateForStartedButNoDocumentsReturned = new QueryState(CosmosString.Create("Started But Haven't Returned Any Documents Yet"));

        private static readonly ReadFeedState ReadFeedNotStartedState = ReadFeedState.Continuation(CosmosString.Create("Started But Haven't Returned Any Documents Yet"));

        private readonly IMonadicDocumentContainer documentContainer;

        public FlakyDocumentContainer(
            IMonadicDocumentContainer documentContainer,
            FailureConfigs failureConfigs)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.failureConfigs = failureConfigs ?? throw new ArgumentNullException(nameof(failureConfigs));
            this.random = new Random();
        }

        public Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return Throttle.ForCreateItem;
            }

            return this.documentContainer.MonadicCreateItemAsync(
                payload,
                cancellationToken);
        }

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return Throttle.ForCreateItem;
            }

            return this.documentContainer.MonadicReadItemAsync(
                partitionKey,
                identifer,
                cancellationToken);
        }

        public async Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
            FeedRangeState<ReadFeedState> feedRangeState,
            ReadFeedExecutionOptions readFeedPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ReadFeedState readFeedState = feedRangeState.State;
            if ((readFeedState != null) && readFeedState.Equals(ReadFeedNotStartedState))
            {
                readFeedState = null;
            }

            if (this.ShouldReturn429())
            {
                return await Throttle.ForReadFeed;
            }

            if (this.ShouldReturnEmptyPage())
            {
                // We can't return a null continuation, since that signals the query has ended.
                ReadFeedState nonNullState = readFeedState ?? ReadFeedNotStartedState;
                return TryCatch<ReadFeedPage>.FromResult(
                        new ReadFeedPage(
                            new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}")),
                            requestCharge: 42,
                            itemCount: 0,
                            activityId: Guid.NewGuid().ToString(),
                            additionalHeaders: null,
                            state: nonNullState));
            }

            Exception failure = await this.ShouldReturnFailure();
            if (failure != null)
            {
                return TryCatch<ReadFeedPage>.FromException(failure);
            }

            return await this.documentContainer.MonadicReadFeedAsync(
                feedRangeState,
                readFeedPaginationOptions,
                trace,
                cancellationToken);
        }

        public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            QueryExecutionOptions queryPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (feedRangeState.State == StateForStartedButNoDocumentsReturned)
            {
                feedRangeState = new FeedRangeState<QueryState>(feedRangeState.FeedRange, null);
            }

            if (this.ShouldReturn429())
            {
                return await Throttle.ForQuery;
            }

            if (this.ShouldReturnEmptyPage())
            {
                return TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: new List<CosmosElement>(),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            cosmosQueryExecutionInfo: default,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: default,
                            state: feedRangeState.State ?? StateForStartedButNoDocumentsReturned,
                            streaming: default));
            }

            Exception failure = await this.ShouldReturnFailure();
            if (failure != null)
            {
                return TryCatch<QueryPage>.FromException(failure);
            }

            return await this.documentContainer.MonadicQueryAsync(
                sqlQuerySpec,
                feedRangeState,
                queryPaginationOptions,
                trace,
                cancellationToken);
        }

        public async Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            FeedRangeState<ChangeFeedState> feedRangeState, 
            ChangeFeedExecutionOptions changeFeedPaginationOptions, 
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return await Throttle.ForChangeFeed;
            }

            if (this.ShouldReturnEmptyPage())
            {
                return TryCatch<ChangeFeedPage>.FromResult(
                        new ChangeFeedSuccessPage(
                            content: new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}")),
                            requestCharge: 42,
                            itemCount: 0,
                            activityId: Guid.NewGuid().ToString(),
                            additionalHeaders: default,
                            state: feedRangeState.State));
            }

            if (this.ShouldThrowException(out Exception exception))
            {
                throw exception;
            }

            Exception failure = await this.ShouldReturnFailure();
            if (failure != null)
            {
                return TryCatch<ChangeFeedPage>.FromException(failure);
            }

            return await this.documentContainer.MonadicChangeFeedAsync(
                feedRangeState,
                changeFeedPaginationOptions,
                trace,
                cancellationToken);
        }

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => this.documentContainer.MonadicSplitAsync(
                feedRange,
                cancellationToken);

        public Task<TryCatch> MonadicMergeAsync(
            FeedRangeInternal feedRange1,
            FeedRangeInternal feedRange2,
            CancellationToken cancellationToken) => this.documentContainer.MonadicMergeAsync(
                feedRange1,
                feedRange2,
                cancellationToken);

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            ITrace trace,
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetChildRangeAsync(
                feedRange,
                trace,
                cancellationToken);

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            ITrace trace,
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetFeedRangesAsync(
                trace,
                cancellationToken);

        public Task<TryCatch> MonadicRefreshProviderAsync(
            ITrace trace,
            CancellationToken cancellationToken) => this.documentContainer.MonadicRefreshProviderAsync(trace, cancellationToken);

        public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(
            ITrace trace,
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetResourceIdentifierAsync(trace, cancellationToken);

        private bool ShouldReturn429() => (this.failureConfigs != null)
            && this.failureConfigs.Inject429s
            && ((this.random.Next() % 2) == 0);

        private bool ShouldReturnEmptyPage() => (this.failureConfigs != null)
            && this.failureConfigs.InjectEmptyPages
            && ((this.random.Next() % 2) == 0);

        private bool ShouldThrowException(out Exception exception)
        {
            exception = this.failureConfigs.ThrowException;
            return this.failureConfigs != null && this.failureConfigs.ThrowException != null;
        }

        private Task<Exception> ShouldReturnFailure()
        {
            return this.failureConfigs == null ? Task.FromResult<Exception>(null) : this.failureConfigs.ReturnFailure();
        }

        public sealed class FailureConfigs
        {
            public delegate Task<Exception> ShouldReturnFailure();

            public FailureConfigs(
                bool inject429s, 
                bool injectEmptyPages,
                Exception throwException = null,
                Exception returnFailure = null)
            {
                this.Inject429s = inject429s;
                this.InjectEmptyPages = injectEmptyPages;
                this.ThrowException = throwException;
                this.ReturnFailure = () => Task.FromResult(returnFailure);
            }

            public FailureConfigs(
                bool inject429s,
                bool injectEmptyPages,
                ShouldReturnFailure shouldReturnFailure)
            {
                this.Inject429s = inject429s;
                this.InjectEmptyPages = injectEmptyPages;
                this.ReturnFailure = shouldReturnFailure;
            }

            public bool Inject429s { get; }

            public bool InjectEmptyPages { get; }

            public Exception ThrowException { get; }

            public ShouldReturnFailure ReturnFailure { get; }
        }
    }
}
