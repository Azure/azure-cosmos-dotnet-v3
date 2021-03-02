//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ChangeFeedIteratorCoreTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Container()
        {
            new ChangeFeedIteratorCore(
                documentContainer: null,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Mode()
        {
            new ChangeFeedIteratorCore(
                documentContainer: Mock.Of<IDocumentContainer>(),
                changeFeedMode: null,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(-1)]
        [DataRow(0)]
        public void ChangeFeedIteratorCore_ValidateOptions(int maxItemCount)
        {
            new ChangeFeedIteratorCore(
                Mock.Of<IDocumentContainer>(),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = maxItemCount
                },
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());
        }

        [TestMethod]
        public void ChangeFeedIteratorCore_HasMoreResultsDefault()
        {
            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                Mock.Of<IDocumentContainer>(),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());
            Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_ReadNextAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());

            int count = 0;
            while (changeFeedIteratorCore.HasMoreResults)
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                count += GetChanges(responseMessage.Content).Count;
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_UpdatesContinuation_On304()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems: 0);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());

            ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.NotModified, responseMessage.StatusCode);
            string continuationToken = responseMessage.Headers.ContinuationToken;

            ResponseMessage responseMessage2 = await changeFeedIteratorCore.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.NotModified, responseMessage.StatusCode);
            string continuationToken2 = responseMessage2.Headers.ContinuationToken;

            Assert.AreNotEqual(continuationToken, continuationToken2);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(inject429s: true, injectEmptyPages: true));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());

            int count = 0;
            int numIterations = 500;
            while (numIterations-- > 0)
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                if (!(responseMessage.IsSuccessStatusCode || responseMessage.StatusCode == HttpStatusCode.NotModified))
                {
                    if (responseMessage.Headers.ContinuationToken != null)
                    {
                        Assert.Fail();
                    }
                }
                else
                {
                    if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                    {
                        count += GetChanges(responseMessage.Content).Count;
                    }
                }

                if (count > numItems)
                {
                    Assert.Fail();
                }
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_HandlesSplitsThroughPipeline()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext());

            int seed = new Random().Next();
            Random random = new Random(seed);

            int count = 0;
            while (changeFeedIteratorCore.HasMoreResults)
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                count += GetChanges(responseMessage.Content).Count;

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                FeedRangeInternal randomRange = ranges[random.Next(ranges.Count)];
                await documentContainer.SplitAsync(randomRange, cancellationToken: default);
            }

            Assert.AreEqual(numItems, count, seed);
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

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
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

        private CosmosClientContext MockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.OperationHelperAsync<ResponseMessage>(
                It.IsAny<string>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<ResponseMessage>>>(),
                It.IsAny<TraceComponent>(),
                It.IsAny<TraceLevel>()))
               .Returns<string, RequestOptions, Func<ITrace, Task<ResponseMessage>>, TraceComponent, TraceLevel>(
                (operationName, requestOptions, func, comp, level) => func(NoOpTrace.Singleton));

            return mockContext.Object;
        }
    }
}
