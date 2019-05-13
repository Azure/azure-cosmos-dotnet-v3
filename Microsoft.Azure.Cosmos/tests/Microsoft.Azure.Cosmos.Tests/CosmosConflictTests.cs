//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

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

        private static CosmosContainerCore GetMockedContainer(Func<CosmosRequestMessage,
            CancellationToken, Task<CosmosResponseMessage>> handlerFunc)
        {
            return new CosmosContainerCore(CosmosConflictTests.GetMockedClientContext(handlerFunc), MockCosmosUtil.CreateMockDatabase().Object, "conflicts");
        }

        private static CosmosClientContext GetMockedClientContext(Func<CosmosRequestMessage,
            CancellationToken, Task<CosmosResponseMessage>> handlerFunc)
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

            return new CosmosClientContextCore(
                client: client,
                clientConfiguration: null,
                cosmosJsonSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: client.RequestHandler,
                documentClient: null,
                documentQueryClient: new Mock<Query.IDocumentQueryClient>().Object);
        }
    }
}
