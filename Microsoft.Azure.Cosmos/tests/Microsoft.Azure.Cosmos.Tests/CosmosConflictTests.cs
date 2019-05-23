//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosConflictTests
    {
        [TestMethod]
        public async Task ConflictsFeedSetsPartitionKeyRangeIdentity()
        {
            CosmosContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.IsNotNull(request.DocumentServiceRequest.PartitionKeyRangeIdentity);
                return TestHandler.ReturnSuccess();
            });
            FeedIterator iterator = container.Conflicts.GetConflictsStreamIterator();
            while (iterator.HasMoreResults)
            {
                CosmosResponseMessage responseMessage = await iterator.FetchNextSetAsync();
            }
        }

        [TestMethod]
        public async Task ReadCurrentGetsCorrectRID()
        {
            const string expectedRID = "something";
            const string partitionKey = "pk";
            // Using "test" as container name because the Mocked DocumentClient has it hardcoded
            Uri expectedRequestUri = new Uri($"/dbs/conflictsDb/colls/test/docs/{expectedRID}", UriKind.Relative);
            CosmosContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.AreEqual(OperationType.Read, request.OperationType);
                Assert.AreEqual(ResourceType.Document, request.ResourceType);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
                return TestHandler.ReturnSuccess();
            });

            CosmosConflictSettings conflictSettings = new CosmosConflictSettings();
            conflictSettings.SourceResourceId = expectedRID;

            await container.Conflicts.ReadCurrentAsync<JObject>(partitionKey, conflictSettings);
        }

        [TestMethod]
        public void ReadConflictContentDeserializesContent()
        {
            CosmosContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                return TestHandler.ReturnSuccess();
            });

            JObject someJsonObject = new JObject();
            someJsonObject["id"] = Guid.NewGuid().ToString();
            someJsonObject["someInt"] = 2;

            CosmosConflictSettings conflictSettings = new CosmosConflictSettings();
            conflictSettings.Content = someJsonObject.ToString();

            Assert.AreEqual(someJsonObject.ToString(), container.Conflicts.ReadConflictContent<JObject>(conflictSettings).ToString());
        }

        [TestMethod]
        public async Task DeleteSendsCorrectPayload()
        {
            const string expectedId = "something";
            const string partitionKey = "pk";
            Uri expectedRequestUri = new Uri($"/dbs/conflictsDb/colls/conflictsColl/conflicts/{expectedId}", UriKind.Relative);
            CosmosContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.AreEqual(OperationType.Delete, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
                return TestHandler.ReturnSuccess();
            });

            CosmosConflictSettings conflictSettings = new CosmosConflictSettings();
            conflictSettings.Id = expectedId;

            await container.Conflicts.DeleteConflictAsync(partitionKey, conflictSettings);
        }

        private static CosmosContainerCore GetMockedContainer(Func<CosmosRequestMessage,
            CancellationToken, Task<CosmosResponseMessage>> handlerFunc)
        {
            return new CosmosContainerCore(CosmosConflictTests.GetMockedClientContext(handlerFunc), MockCosmosUtil.CreateMockDatabase("conflictsDb").Object, "conflictsColl");
        }

        private static CosmosClientContext GetMockedClientContext(
            Func<CosmosRequestMessage, CancellationToken, Task<CosmosResponseMessage>> handlerFunc)
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = MockCosmosUtil.GetPartitionRoutingHelperMock("0");
            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(client, partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(handlerFunc);
            partitionKeyRangeHandler.InnerHandler = testHandler;

            CosmosRequestHandler handler = client.RequestHandler.InnerHandler;
            while (handler != null)
            {
                if (handler.InnerHandler is RouterHandler)
                {
                    handler.InnerHandler = new RouterHandler(partitionKeyRangeHandler, testHandler);
                    break;
                }

                handler = handler.InnerHandler;
            }

            CosmosJsonSerializer cosmosJsonSerializer = new CosmosDefaultJsonSerializer();

            CosmosResponseFactory responseFactory = new CosmosResponseFactory(cosmosJsonSerializer);

            return new CosmosClientContextCore(
                client: client,
                clientOptions: null,
                cosmosJsonSerializer: cosmosJsonSerializer,
                cosmosResponseFactory: responseFactory,
                requestHandler: client.RequestHandler,
                documentClient: new MockDocumentClient(),
                documentQueryClient: new Mock<Query.IDocumentQueryClient>().Object);
        }
    }
}
