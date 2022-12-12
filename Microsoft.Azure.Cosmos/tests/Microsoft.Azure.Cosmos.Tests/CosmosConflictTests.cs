//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Tracing;

    [TestClass]
    public class CosmosConflictTests
    {
        [TestMethod]
        public async Task ConflictsFeedSetsPartitionKeyRangeIdentity()
        {
            ContainerInternal container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.IsNotNull(request.ToDocumentServiceRequest().PartitionKeyRangeIdentity);
                return TestHandler.ReturnSuccess();
            });

            FeedIterator iterator = container.Conflicts.GetConflictQueryStreamIterator();
            while (iterator.HasMoreResults)
            {
                ResponseMessage responseMessage = await iterator.ReadNextAsync();
            }
        }

        [TestMethod]
        public async Task ReadCurrentGetsCorrectRID()
        {
            const string expectedRID = "something";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("pk");
            // Using "test" as container name because the Mocked DocumentClient has it hardcoded
            Uri expectedRequestUri = new Uri($"dbs/V4lVAA==/colls/V4lVAMl0wuQ=/docs/{expectedRID}", UriKind.Relative);
            ContainerInternal container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.AreEqual(OperationType.Read, request.OperationType);
                Assert.AreEqual(ResourceType.Document, request.ResourceType);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
                return TestHandler.ReturnSuccess();
            });

            ConflictProperties conflictSettings = new ConflictProperties();
            conflictSettings.SourceResourceId = expectedRID;

            await container.Conflicts.ReadCurrentAsync<JObject>(conflictSettings, partitionKey);
        }

        [TestMethod]
        public void ReadConflictContentDeserializesContent()
        {
            ContainerInternal container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                return TestHandler.ReturnSuccess();
            });

            JObject someJsonObject = new JObject();
            someJsonObject["id"] = Guid.NewGuid().ToString();
            someJsonObject["someInt"] = 2;

            ConflictProperties conflictSettings = new ConflictProperties();
            conflictSettings.Content = someJsonObject.ToString();

            Assert.AreEqual(someJsonObject.ToString(), container.Conflicts.ReadConflictContent<JObject>(conflictSettings).ToString());
        }

        [TestMethod]
        public async Task DeleteSendsCorrectPayload()
        {
            const string expectedId = "something";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("pk");
            Uri expectedRequestUri = new Uri($"/dbs/myDb/colls/conflictsColl/conflicts/{expectedId}", UriKind.Relative);
            ContainerInternal container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.AreEqual(OperationType.Delete, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
                return TestHandler.ReturnSuccess();
            });

            ConflictProperties conflictSettings = new ConflictProperties();
            conflictSettings.Id = expectedId;

            await container.Conflicts.DeleteAsync(conflictSettings, partitionKey);
        }

        private static ContainerInternal GetMockedContainer(Func<RequestMessage,
            CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            CosmosClientContext clientContext = CosmosConflictTests.GetMockedClientContext(handlerFunc);
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: "conflictsColl");
            DatabaseInternal database = MockCosmosUtil.CreateMockDatabase("conflictsDb").Object;
            string monitoredContainerRid = "V4lVAMl0wuQ=";
            mockedContainer.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(monitoredContainerRid);
            mockedContainer.Setup(c => c.Database).Returns(database);
            mockedContainer.Setup(c => c.GetReadFeedIterator(It.IsAny<QueryDefinition>(), It.IsAny<QueryRequestOptions>(), It.IsAny<string>(), It.Is<ResourceType>(r => r == ResourceType.Conflict), It.IsAny<string>(), It.IsAny<int>()))
                .Returns(
                    (QueryDefinition qd, QueryRequestOptions o, string link, ResourceType t, string ct, int p) => new ContainerInlineCore(clientContext, database, "conflictsColl").GetReadFeedIterator(qd, o, link, t, ct, p));
            mockedContainer.Setup(c => c.ClientContext).Returns(clientContext);
            mockedContainer.Setup(c => c.Conflicts).Returns(new ConflictsInlineCore(clientContext, mockedContainer.Object));
            return mockedContainer.Object;
        }

        private static CosmosClientContext GetMockedClientContext(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            CosmosClientContext clientContext = ClientContextCore.Create(
               client,
               new MockDocumentClient(),
               new CosmosClientOptions());

            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = MockCosmosUtil.GetPartitionRoutingHelperMock("0");
            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(client, partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(handlerFunc);
            partitionKeyRangeHandler.InnerHandler = testHandler;

            RequestHandler handler = clientContext.RequestHandler.InnerHandler;
            while (handler != null)
            {
                if (handler.InnerHandler is RouterHandler)
                {
                    handler.InnerHandler = new RouterHandler(partitionKeyRangeHandler, testHandler);
                    break;
                }

                handler = handler.InnerHandler;
            }

            return clientContext;
        }
    }
}
