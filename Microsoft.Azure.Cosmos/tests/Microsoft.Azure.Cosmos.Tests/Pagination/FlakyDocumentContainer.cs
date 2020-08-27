//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Implementation of <see cref="IMonadicDocumentContainer"/> that composes another <see cref="IMonadicDocumentContainer"/> and randomly adds in exceptions.
    /// This is useful for mocking throttles and other edge cases like empty pages.
    /// </summary>
    internal sealed class FlakyDocumentContainer : IMonadicDocumentContainer
    {
        private readonly FailureConfigs failureConfigs;
        private readonly Random random;

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
                return ThrottleForCreateItem;
            }

            return this.documentContainer.MonadicCreateItemAsync(
                payload,
                cancellationToken);
        }

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            Guid identifer,
            CancellationToken cancellationToken)
        {
            if (this.ShouldReturn429())
            {
                return ThrottleForCreateItem;
            }

            return this.documentContainer.MonadicReadItemAsync(
                partitionKey,
                identifer,
                cancellationToken);
        }

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

            return this.documentContainer.MonadicReadFeedAsync(
                partitionKeyRangeId,
                resourceIdentifer,
                pageSize,
                cancellationToken);
        }

        public Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken) => this.documentContainer.MonadicSplitAsync(
                partitionKeyRangeId,
                cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetChildRangeAsync(
                partitionKeyRange,
                cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.documentContainer.MonadicGetFeedRangesAsync(
                cancellationToken);

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
