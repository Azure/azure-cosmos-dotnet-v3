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
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Documents;

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

        private static readonly string ContinuationForStartedButNoDocumentsReturned = "Started But Haven't Returned Any Documents Yet";

        private static readonly ReadFeedState ReadFeedNotStartedState = ReadFeedState.Continuation(CosmosString.Create(ContinuationForStartedButNoDocumentsReturned));

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

        public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
            ReadFeedState readFeedState,
            FeedRangeInternal feedRange,
            QueryRequestOptions queryRequestOptions,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if ((readFeedState != null) && readFeedState.Equals(ReadFeedNotStartedState))
            {
                readFeedState = null;
            }

            if (this.ShouldReturn429())
            {
                return Throttle.ForReadFeed;
            }

            if (this.ShouldReturnEmptyPage())
            {
                // We can't return a null continuation, since that signals the query has ended.
                ReadFeedState nonNullState = readFeedState ?? ReadFeedNotStartedState;
                return Task.FromResult(
                    TryCatch<ReadFeedPage>.FromResult(
                        new ReadFeedPage(
                            new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}")),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            diagnostics: CosmosDiagnosticsContext.Create(default),
                            nonNullState)));
            }

            return this.documentContainer.MonadicReadFeedAsync(
                readFeedState,
                feedRange,
                queryRequestOptions,
                pageSize,
                cancellationToken);
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (continuationToken == ContinuationForStartedButNoDocumentsReturned)
            {
                continuationToken = null;
            }

            if (this.ShouldReturn429())
            {
                return Throttle.ForQuery;
            }

            if (this.ShouldReturnEmptyPage())
            {
                string nonNullContinuationToken;
                if (continuationToken == null)
                {
                    // We can't return a null continuation, since that signals the query has ended.
                    nonNullContinuationToken = ContinuationForStartedButNoDocumentsReturned;
                }
                else
                {
                    nonNullContinuationToken = continuationToken;
                }

                return Task.FromResult(
                    TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: new List<CosmosElement>(),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: "[]".Length,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            state: new QueryState(CosmosString.Create(nonNullContinuationToken)))));
            }

            return this.documentContainer.MonadicQueryAsync(
                sqlQuerySpec,
                continuationToken,
                feedRange,
                pageSize,
                cancellationToken);
        }

        public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            ChangeFeedState state, 
            FeedRangeInternal feedRange, 
            int pageSize, 
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return Throttle.ForChangeFeed;
            }

            if (this.ShouldReturnEmptyPage())
            {
                return Task.FromResult(
                    TryCatch<ChangeFeedPage>.FromResult(
                        new ChangeFeedSuccessPage(
                            content: new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}")),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            state: state)));
            }

            return this.documentContainer.MonadicChangeFeedAsync(
                state,
                feedRange,
                pageSize,
                cancellationToken);
        }

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => this.documentContainer.MonadicSplitAsync(
                feedRange,
                cancellationToken);

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetChildRangeAsync(
                feedRange,
                cancellationToken);

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetFeedRangesAsync(
                cancellationToken);

        public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetResourceIdentifierAsync(cancellationToken);

        private bool ShouldReturn429() => (this.failureConfigs != null)
            && this.failureConfigs.Inject429s
            && ((this.random.Next() % 2) == 0);

        private bool ShouldReturnEmptyPage() => (this.failureConfigs != null)
            && this.failureConfigs.InjectEmptyPages
            && ((this.random.Next() % 2) == 0);

        public sealed class FailureConfigs
        {
            public FailureConfigs(bool inject429s, bool injectEmptyPages)
            {
                this.Inject429s = inject429s;
                this.InjectEmptyPages = injectEmptyPages;
            }

            public bool Inject429s { get; }

            public bool InjectEmptyPages { get; }
        }
    }
}
