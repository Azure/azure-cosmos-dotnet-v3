//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ReadFeedIteratorCoreTests
    {
        [TestMethod]
        public void ReadFeedIteratorCore_HasMoreResultsDefault()
        {
            FeedIterator iterator = CreateReadFeedIterator(
                Mock.Of<IDocumentContainer>(),
                default,
                default);
            Assert.IsTrue(iterator.HasMoreResults);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadNextAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            FeedIterator iterator = CreateReadFeedIterator(
                documentContainer,
                continuationToken: null,
                pageSize: 10);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                ResponseMessage message = await iterator.ReadNextAsync();
                CosmosArray documents = GetDocuments(message.Content);
                count += documents.Count;
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadNextAsync_WithContinuationToken()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            int count = 0;
            string continuationToken = null;
            do
            {
                FeedIterator iterator = CreateReadFeedIterator(
                    documentContainer,
                    continuationToken: continuationToken,
                    pageSize: 10);
                ResponseMessage message = await iterator.ReadNextAsync();
                Assert.AreEqual(HttpStatusCode.OK, message.StatusCode, message.ErrorMessage);
                CosmosArray documents = GetDocuments(message.Content);
                count += documents.Count;
                continuationToken = message.ContinuationToken;
            } while (continuationToken != null);

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(inject429s: true, injectEmptyPages: true));

            int count = 0;
            string continuationToken = null;
            bool needsToRetry;
            do
            {
                FeedIterator iterator = CreateReadFeedIterator(
                    documentContainer,
                    continuationToken: continuationToken,
                    pageSize: 10);
                ResponseMessage message = await iterator.ReadNextAsync();
                if (message.IsSuccessStatusCode)
                {
                    needsToRetry = false;
                    CosmosArray documents = GetDocuments(message.Content);
                    count += documents.Count;
                    continuationToken = message.ContinuationToken;
                }
                else if ((int)message.StatusCode == 429)
                {
                    Assert.IsNull(message.ContinuationToken);
                    needsToRetry = true;
                }
                else
                {
                    Assert.Fail();
                    return;
                }
            } while (continuationToken != null || needsToRetry);

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_HandlesSplitsThroughPipeline()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            FeedIterator iterator = CreateReadFeedIterator(
                documentContainer,
                continuationToken: null,
                pageSize: 10);

            Random random = new Random();
            int count = 0;
            while (iterator.HasMoreResults)
            {
                ResponseMessage message = await iterator.ReadNextAsync();
                CosmosArray documents = GetDocuments(message.Content);
                count += documents.Count;

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                FeedRangeEpk rangeToSplit = ranges[random.Next(0, ranges.Count)];
                await documentContainer.SplitAsync(rangeToSplit, cancellationToken: default);
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_UsesExplicitFeedRange()
        {
            const int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default);
            FeedRangeInternal expectedFeedRange = ranges[0];

            int expectedCount = await DrainIteratorAsync(CreateReadFeedIterator(
                documentContainer,
                continuationToken: null,
                pageSize: 10,
                queryRequestOptions: new QueryRequestOptions
                {
                    FeedRange = expectedFeedRange,
                }));
            int actualCount = await DrainIteratorAsync(CreateReadFeedIterator(
                documentContainer,
                continuationToken: null,
                pageSize: 10,
                feedRange: expectedFeedRange));

            Assert.AreEqual(expectedCount, actualCount);
            Assert.IsTrue(actualCount < numItems);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_PartitionKeyPrecedesExplicitFeedRange()
        {
            string[] expectedIds = new[]
            {
                "tenant-a-1",
                "tenant-a-2",
                "tenant-a-3",
            };
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                expectedIds
                    .Select(id => CosmosObject.Parse($"{{\"id\":\"{id}\",\"pk\":\"tenant-a\"}}"))
                    .Concat(new[]
                    {
                        CosmosObject.Parse("{\"id\":\"tenant-b-1\",\"pk\":\"tenant-b\"}"),
                        CosmosObject.Parse("{\"id\":\"tenant-b-2\",\"pk\":\"tenant-b\"}"),
                    })
                    .ToList());
            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey("tenant-a"),
            };

            List<string> actualIds = new List<string>();
            FeedIterator iterator = CreateReadFeedIterator(
                documentContainer,
                continuationToken: null,
                pageSize: 1,
                queryRequestOptions: requestOptions,
                feedRange: FeedRangeEpk.FullRange);
            ResponseMessage firstResponse = await iterator.ReadNextAsync();
            actualIds.AddRange(GetDocumentIds(firstResponse.Content));
            Assert.IsNotNull(firstResponse.ContinuationToken);
            iterator.Dispose();

            iterator = CreateReadFeedIterator(
                documentContainer,
                continuationToken: firstResponse.ContinuationToken,
                pageSize: 1,
                queryRequestOptions: requestOptions,
                feedRange: FeedRangeEpk.FullRange);
            while (iterator.HasMoreResults)
            {
                ResponseMessage response = await iterator.ReadNextAsync();
                actualIds.AddRange(GetDocumentIds(response.Content));
            }

            CollectionAssert.AreEquivalent(expectedIds, actualIds);
        }

        [DataTestMethod]
        [DataRow(true, "AA000", "CC00")]
        [DataRow(false, "AA00", "CC000")]
        public void ReadFeedIteratorCore_ContinuationUsesEpkHull(
            bool useLengthAwareRangeComparer,
            string expectedMin,
            string expectedMax)
        {
            Microsoft.Azure.Cosmos.PartitionKey partitionKey =
                new Microsoft.Azure.Cosmos.PartitionKey("tenant-a");
            CrossFeedRangeState<ReadFeedState> state =
                new CrossFeedRangeState<ReadFeedState>(
                    new FeedRangeState<ReadFeedState>[]
                    {
                        new FeedRangeState<ReadFeedState>(
                            new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    "AA000",
                                    "CC00",
                                    isMinInclusive: true,
                                    isMaxInclusive: false)),
                            ReadFeedState.Beginning()),
                        new FeedRangeState<ReadFeedState>(
                            new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    "AA00",
                                    "CC000",
                                    isMinInclusive: true,
                                    isMaxInclusive: false)),
                            ReadFeedState.Beginning()),
                    });

            FeedRangeEpk continuationFeedRange =
                (FeedRangeEpk)ReadFeedIteratorCore.GetContinuationFeedRange(
                    new FeedRangePartitionKey(partitionKey),
                    state,
                    useLengthAwareRangeComparer);

            Assert.AreEqual(expectedMin, continuationFeedRange.Range.Min);
            Assert.AreEqual(expectedMax, continuationFeedRange.Range.Max);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ResponseHeaders()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            FeedIterator iterator = CreateReadFeedIterator(
                documentContainer,
                continuationToken: null,
                pageSize: 10);

            while (iterator.HasMoreResults)
            {
                ResponseMessage message = await iterator.ReadNextAsync();
                _ = GetDocuments(message.Content);

                Assert.IsTrue(message.Headers.AllKeys().Contains("test-header"));
            }
        }

        private static CosmosArray GetDocuments(Stream stream)
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

        private static List<string> GetDocumentIds(Stream stream)
        {
            return GetDocuments(stream)
                .Select(document => ((CosmosString)((CosmosObject)document)["id"]).Value.ToString())
                .ToList();
        }

        private static FeedIteratorInternal CreateReadFeedIterator(
            IDocumentContainer documentContainer,
            string continuationToken,
            int pageSize,
            QueryRequestOptions queryRequestOptions = null,
            FeedRangeInternal feedRange = null,
            bool useLengthAwareRangeComparer = true)
        {
            return new ReadFeedIteratorCore(
                documentContainer,
                queryRequestOptions: queryRequestOptions,
                continuationToken: continuationToken,
                readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: pageSize),
                cancellationToken: default,
                container: null,
                useLengthAwareRangeComparer: useLengthAwareRangeComparer,
                feedRange: feedRange);
        }

        private static async Task<int> DrainIteratorAsync(FeedIterator iterator)
        {
            int count = 0;
            while (iterator.HasMoreResults)
            {
                ResponseMessage message = await iterator.ReadNextAsync();
                count += GetDocuments(message.Content).Count;
            }

            return count;
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
        {
            return await CreateDocumentContainerAsync(
                Enumerable.Range(0, numItems)
                    .Select(i => CosmosObject.Parse($"{{\"pk\" : {i} }}"))
                    .ToList(),
                failureConfigs);
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            IReadOnlyList<CosmosObject> items,
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

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            foreach (CosmosObject item in items)
            {
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