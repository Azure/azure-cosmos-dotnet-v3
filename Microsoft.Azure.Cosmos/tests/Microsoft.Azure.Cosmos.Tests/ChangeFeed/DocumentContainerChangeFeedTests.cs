//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class DocumentContainerChangeFeedTests
    {
        [TestMethod]
        public async Task EmptyContainerTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 0);
            List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
            TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                ChangeFeedState.Beginning(),
                new FeedRangePartitionKeyRange(ranges[0].Id),
                pageSize: 10,
                cancellationToken: default);

            Assert.IsTrue(monadicChangeFeedPage.Failed);
            if (!(monadicChangeFeedPage.InnerMostException is CosmosException cosmosException))
            {
                Assert.Fail("Wrong exception type.");
                throw new Exception();
            }

            Assert.AreEqual(HttpStatusCode.NotModified, cosmosException.StatusCode);
        }

        [TestMethod]
        public async Task StartFromBeginingTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 10);
            List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);

            // Should get back the all the documents inserted so far
            ChangeFeedState resumeState = default;
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Beginning(),
                    new FeedRangePartitionKeyRange(ranges[0].Id),
                    pageSize: int.MaxValue,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);

                resumeState = monadicChangeFeedPage.Result.State;
            }

            // No more changes left
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    resumeState,
                    new FeedRangePartitionKeyRange(ranges[0].Id),
                    pageSize: 10,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Failed);
                if (!(monadicChangeFeedPage.InnerMostException is CosmosException cosmosException))
                {
                    Assert.Fail("Wrong exception type.");
                    throw new Exception();
                }

                Assert.AreEqual(HttpStatusCode.NotModified, cosmosException.StatusCode);
            }
        }

        [TestMethod]
        public async Task StartFromTimeTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 10);
            List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);

            DateTime now = DateTime.UtcNow;
            // No changes let
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Time(now),
                    new FeedRangePartitionKeyRange(ranges[0].Id),
                    pageSize: 10,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Failed);
                if (!(monadicChangeFeedPage.InnerMostException is CosmosException cosmosException))
                {
                    Assert.Fail("Wrong exception type.");
                    throw new Exception();
                }

                Assert.AreEqual(HttpStatusCode.NotModified, cosmosException.StatusCode);
            }

            // Insert some items
            for (int i = 0; i < 10; i++)
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

            // Now we should be able to see the changes
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Time(now),
                    new FeedRangePartitionKeyRange(ranges[0].Id),
                    pageSize: int.MaxValue,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
            }
        }

        [TestMethod]
        public async Task StartFromNowAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 10);
            List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);

            ChangeFeedState resumeState = default;
            // No changes starting from now
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Now(),
                    new FeedRangePartitionKeyRange(ranges[0].Id),
                    pageSize: 10,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Failed);
                if (!(monadicChangeFeedPage.InnerMostException is CosmosException cosmosException))
                {
                    Assert.Fail("Wrong exception type.");
                    throw new Exception();
                }

                Assert.AreEqual(HttpStatusCode.NotModified, cosmosException.StatusCode);
                resumeState = ChangeFeedState.Continuation(cosmosException.Headers.ContinuationToken);
            }

            // Insert some items
            for (int i = 0; i < 10; i++)
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

            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    resumeState,
                    new FeedRangePartitionKeyRange(ranges[0].Id),
                    pageSize: 10,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
            }
        }

        [TestMethod]
        public async Task ReadChangesAcrossSplitsAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 100);
            List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
            long numRecords = (await documentContainer.ReadFeedAsync(
                int.Parse(ranges[0].Id),
                ResourceId.Empty,
                pageSize: int.MaxValue,
                cancellationToken: default)).Records.Count;

            await documentContainer.SplitAsync(int.Parse(ranges[0].Id), cancellationToken: default);

            List<PartitionKeyRange> children = await documentContainer.GetChildRangeAsync(ranges[0], cancellationToken: default);

            long sumOfChildCounts = 0;
            foreach (PartitionKeyRange child in children)
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Beginning(),
                    new FeedRangePartitionKeyRange(child.Id),
                    pageSize: 1000,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);

                MemoryStream memoryStream = new MemoryStream();
                monadicChangeFeedPage.Result.Content.CopyTo(memoryStream);
                CosmosObject response = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                long childCount = Number64.ToLong(((CosmosNumber)response["_count"]).Value);
                sumOfChildCounts += childCount;
            }

            Assert.AreEqual(numRecords, sumOfChildCounts);
        }

        private async Task<IDocumentContainer> CreateDocumentContainerAsync(
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

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            await documentContainer.SplitAsync(partitionKeyRangeId: 1, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 2, cancellationToken: default);

            await documentContainer.SplitAsync(partitionKeyRangeId: 3, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 4, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 5, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 6, cancellationToken: default);

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
