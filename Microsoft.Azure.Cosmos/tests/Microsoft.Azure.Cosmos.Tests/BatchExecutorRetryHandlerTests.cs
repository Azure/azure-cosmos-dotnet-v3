//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class BatchExecutorRetryHandlerTests
    {
        private static CosmosSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();

        [TestMethod]
        public async Task NotRetryOnSuccess()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            mockContext.Setup(x => x.ClientOptions).Returns(new CosmosClientOptions());
            Mock<BatchAsyncContainerExecutor> mockedExecutor = new Mock<BatchAsyncContainerExecutor>();
            mockedExecutor
                    .Setup(e => e.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BatchOperationResult(HttpStatusCode.OK));

            BatchExecutorRetryHandler batchExecutorRetryHandler = new BatchExecutorRetryHandler(mockContext.Object, mockedExecutor.Object);
            ResponseMessage result = await batchExecutorRetryHandler.SendAsync(CreateItemBatchOperation());
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task RetriesOn429()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            mockContext.Setup(x => x.ClientOptions).Returns(new CosmosClientOptions());
            Mock<BatchAsyncContainerExecutor> mockedExecutor = new Mock<BatchAsyncContainerExecutor>();
            mockedExecutor
                    .SetupSequence(e => e.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BatchOperationResult((HttpStatusCode)StatusCodes.TooManyRequests))
                    .ReturnsAsync(new BatchOperationResult(HttpStatusCode.OK));

            BatchExecutorRetryHandler batchExecutorRetryHandler = new BatchExecutorRetryHandler(mockContext.Object, mockedExecutor.Object);
            ResponseMessage result = await batchExecutorRetryHandler.SendAsync(CreateItemBatchOperation());
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        private static ItemBatchOperation CreateItemBatchOperation()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            return new ItemBatchOperation(OperationType.Create, /* index */ 0, new Cosmos.PartitionKey(testItem.pk), testItem.id, cosmosDefaultJsonSerializer.ToStream(testItem));
        }
    }
}
