// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal abstract class DocumentContainer : IFeedRangeProvider
    {
        private static readonly CosmosException RequestRateTooLargeException = new CosmosException(
            message: "Request Rate Too Large",
            statusCode: (System.Net.HttpStatusCode)429,
            subStatusCode: default,
            activityId: Guid.NewGuid().ToString(),
            requestCharge: default);

        private static readonly Task<TryCatch<Record>> ThrottleForCreateItem = Task.FromResult(
            TryCatch<Record>.FromException(
                RequestRateTooLargeException));

        private static readonly Task<TryCatch<DocumentContainerPage>> ThrottleForFeedOperation = Task.FromResult(
            TryCatch<DocumentContainerPage>.FromException(
                RequestRateTooLargeException));

        private static readonly PartitionKeyRange FullRange = new PartitionKeyRange()
        {
            MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
        };

        private readonly FailureConfigs failureConfigs;
        private readonly Random random;

        protected DocumentContainer(FailureConfigs failureConfigs = null)
        {
            this.failureConfigs = failureConfigs;
            this.random = new Random();
        }

        protected abstract Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeImplementationAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => this.MonadicGetChildRangeImplementationAsync(
                partitionKeyRange,
                cancellationToken);

        public Task<List<PartitionKeyRange>> GetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicGetChildRangeAsync(
                    partitionKeyRange,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(
                DocumentContainer.FullRange,
                cancellationToken);

        public Task<List<PartitionKeyRange>> GetFeedRangesAsync(
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicGetFeedRangesAsync(
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<Record>> MonadicCreateItemImplementationAsync(
            CosmosObject payload,
            CancellationToken cancellationToken);

        public Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForCreateItem;
            }

            return this.MonadicCreateItemImplementationAsync(
                payload,
                cancellationToken);
        }

        public Task<Record> CreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicCreateItemAsync(
                    payload,
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<Record>> MonadicReadItemImplementationAsync(
            CosmosElement partitionKey,
            Guid identifer,
            CancellationToken cancellationToken);

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            Guid identifer,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForCreateItem;
            }

            return this.MonadicReadItemImplementationAsync(
                partitionKey,
                identifer,
                cancellationToken);
        }

        public Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            Guid identifier,
            CancellationToken cancellationToken) => TryCatch<Record>.UnsafeGetResultAsync(
                this.MonadicReadItemAsync(
                    partitionKey,
                    identifier,
                    cancellationToken),
                cancellationToken);

        protected abstract Task<TryCatch<DocumentContainerPage>> MonadicReadFeedImplementationAsync(
            int partitionKeyRangeId,
            long resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken);

        public Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForFeedOperation;
            }

            if (this.ShouldReturnEmptyPage())
            {
                return Task.FromResult(
                    TryCatch<DocumentContainerPage>.FromResult(
                        new DocumentContainerPage(
                            new List<Record>(),
                            new DocumentContainerState(resourceIdentifer))));
            }

            return this.MonadicReadFeedImplementationAsync(
                partitionKeyRangeId,
                resourceIdentifer,
                pageSize,
                cancellationToken);
        }

        public Task<DocumentContainerPage> ReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<DocumentContainerPage>.UnsafeGetResultAsync(
                this.MonadicReadFeedAsync(
                    partitionKeyRangeId,
                    resourceIdentifier,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        public abstract Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken);

        public Task SplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken) => TryCatch.UnsafeWaitAsync(
                this.MonadicSplitAsync(
                    partitionKeyRangeId,
                    cancellationToken),
                cancellationToken);
        private bool ShouldReturn429() => (this.failureConfigs != null) && this.failureConfigs.Inject429s && ((this.random.Next() % 2) == 0);

        private bool ShouldReturnEmptyPage() => (this.failureConfigs != null) && this.failureConfigs.InjectEmptyPages && ((this.random.Next() % 2) == 0);

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
