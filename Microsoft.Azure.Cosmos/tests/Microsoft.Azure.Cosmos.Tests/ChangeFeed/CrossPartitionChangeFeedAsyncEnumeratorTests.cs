//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using System.IO;
    using Microsoft.Azure.Cosmos.Tracing;
    using System.Collections.Immutable;

    [TestClass]
    public sealed class CrossPartitionChangeFeedAsyncEnumeratorTests
    {
        [TestMethod]
        public async Task NoChangesAsync()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems: 0);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new ChangeFeedRequestOptions(),
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                    }),
                cancellationToken: default);

            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.IsTrue(enumerator.Current.Succeeded);
            Assert.IsTrue(enumerator.Current.Result.Page is ChangeFeedNotModifiedPage);
            Assert.IsNotNull(enumerator.Current.Result.State);
        }

        [TestMethod]
        public async Task SomeChangesAsync()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems: 1);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new ChangeFeedRequestOptions(),
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                    }),
                cancellationToken: default);

            // First page should be true and skip the 304 not modified
            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.IsTrue(enumerator.Current.Succeeded);
            Assert.IsTrue(enumerator.Current.Result.Page is ChangeFeedSuccessPage);

            // Second page should surface up the 304
            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.IsTrue(enumerator.Current.Succeeded);
            Assert.IsTrue(enumerator.Current.Result.Page is ChangeFeedNotModifiedPage);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Use Continuations: false")]
        [DataRow(true, DisplayName = "Use Continuations: true")]
        public async Task StartFromBeginningAsync(bool useContinuations)
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new ChangeFeedRequestOptions(),
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                    }),
                cancellationToken: default);

            int globalCount = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));
            Assert.AreEqual(numItems, globalCount);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Use Continuations: false")]
        [DataRow(true, DisplayName = "Use Continuations: true")]
        public async Task StartFromTimeAsync(bool useContinuations)
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new ChangeFeedRequestOptions(),
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Time(DateTime.UtcNow))
                    }),
                cancellationToken: default);

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            int globalCount = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));

            Assert.AreEqual(numItems, globalCount);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Use Continuations: false")]
        [DataRow(true, DisplayName = "Use Continuations: true")]
        public async Task StartFromNowAsync(bool useContinuations)
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new ChangeFeedRequestOptions(),
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Now())
                    }),
                cancellationToken: default);

            Assert.AreEqual(0, await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator)));

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            int globalCount = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));
            Assert.AreEqual(numItems, globalCount);
        }

        private static async Task<int> DrainUntilNotModifedAsync(CrossPartitionChangeFeedAsyncEnumerator enumerator)
        {
            int globalCount = 0;
            while (await enumerator.MoveNextAsync())
            {
                Assert.IsTrue(enumerator.Current.Succeeded);
                if (!(enumerator.Current.Result.Page is ChangeFeedSuccessPage changeFeedSuccessPage))
                {
                    break;
                }

                globalCount += GetResponseCount(changeFeedSuccessPage.Content);
            }

            return globalCount;
        }

        private static async Task<int> DrainWithUntilNotModifiedWithContinuationTokens(
            IDocumentContainer documentContainer, 
            CrossPartitionChangeFeedAsyncEnumerator enumerator)
        {
            List<CosmosElement> globalChanges = new List<CosmosElement>();
            while (true)
            {
                if (!await enumerator.MoveNextAsync())
                {
                    throw new InvalidOperationException();
                }

                Assert.IsTrue(enumerator.Current.Succeeded);

                if (!(enumerator.Current.Result.Page is ChangeFeedSuccessPage changeFeedSuccessPage))
                {
                    break;
                }

                CosmosArray changes = GetChanges(changeFeedSuccessPage.Content);
                globalChanges.AddRange(changes);

                enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                    documentContainer,
                    new ChangeFeedRequestOptions(),
                    enumerator.Current.Result.State,
                    cancellationToken: default);
            }

            return globalChanges.Count;
        }

        private static int GetResponseCount(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                CosmosObject element = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                if (!element.TryGetValue("_count", out CosmosElement value))
                {
                    Assert.Fail();
                }

                return (int)Number64.ToLong(((CosmosNumber)value).Value);
            }
        }

        private static CosmosArray GetChanges(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                CosmosObject element = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                if (!element.TryGetValue("Documents", out CosmosArray value))
                {
                    Assert.Fail();
                }

                return value;
            }
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                    {
                        "/pk"
                    },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }
    }
}
