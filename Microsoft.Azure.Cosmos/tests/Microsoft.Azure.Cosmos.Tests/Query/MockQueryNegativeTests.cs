//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MockQueryNegativeTests
    {
        [TestMethod]
        public async Task TestSubStatusCode()
        {
            ResponseMessage failedResponse = new ResponseMessage(statusCode: System.Net.HttpStatusCode.BadRequest);
            failedResponse.Headers.SubStatusCode = Documents.SubStatusCodes.ReadSessionNotAvailable;
            failedResponse.Headers.ActivityId = new Guid("A9F6D58B-8F9A-45B4-9887-F5C710E025DD").ToString();

            Mock<RequestHandler> mockHandler = new Mock<RequestHandler>();
            mockHandler.Setup(x => x.SendAsync(It.IsAny<RequestMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(failedResponse));

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder => builder.AddCustomHandlers(mockHandler.Object));
            Container container = client.GetContainer("TestDb", "TestContainer");
            FeedIterator feedIterator = container.GetItemQueryStreamIterator("select * from t");
            Assert.IsTrue(feedIterator.HasMoreResults);
            ResponseMessage responseMessage = await feedIterator.ReadNextAsync();
            Assert.AreEqual(failedResponse.StatusCode, responseMessage.StatusCode);
            Assert.AreEqual(Documents.SubStatusCodes.ReadSessionNotAvailable, responseMessage.Headers.SubStatusCode);
            Assert.AreEqual(failedResponse.Headers.ActivityId, responseMessage.Headers.ActivityId);
        }
    }
}