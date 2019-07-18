//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ChangeFeedResultSetIteratorTests
    {
        [TestMethod]
        public async Task ContinuationTokenIsNotUpdatedOnFails()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            mockContext.Setup(x => x.CosmosSerializer).Returns(new CosmosJsonSerializerCore());
            mockContext.Setup(x => x.Client).Returns(client);

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.Headers.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(HttpStatusCode.NotFound);
            secondResponse.Headers.ETag = "ShouldNotContainThis";
            secondResponse.ErrorMessage = "something";

            mockContext.SetupSequence(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(firstResponse))
                .Returns(Task.FromResult(secondResponse));

            ChangeFeedResultSetIteratorCore iterator = new ChangeFeedResultSetIteratorCore(
                mockContext.Object, (ContainerCore)client.GetContainer("myDb", "myColl"), null, 10, new ChangeFeedRequestOptions());
            ResponseMessage firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(firstResponse.Headers.ETag), "Response should contain the first continuation");

            ResponseMessage secondRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(secondRequest.Headers.ContinuationToken.Contains(firstResponse.Headers.ETag), "Response should contain the first continuation");
            Assert.IsFalse(secondRequest.Headers.ContinuationToken.Contains(secondResponse.Headers.ETag), "Response should NOT contain the second continuation");
        }
    }
}
