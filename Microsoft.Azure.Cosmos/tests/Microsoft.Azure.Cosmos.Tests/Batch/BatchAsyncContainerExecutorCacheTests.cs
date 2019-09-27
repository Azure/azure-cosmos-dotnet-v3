//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]    
    public class BatchAsyncContainerExecutorCacheTests
    {
        [TestMethod]
        public async Task ConcurrentGet_ReturnsSameExecutorInstance()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            CosmosClientContext context = new ClientContextCore(
                client: mockClient.Object,
                clientOptions: new CosmosClientOptions() { AllowBulkExecution = true },
                userJsonSerializer: null,
                defaultJsonSerializer: null,
                sqlQuerySpecSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: null);

            DatabaseCore db = new DatabaseCore(context, "test");

            List<Task<ContainerCore>> tasks = new List<Task<ContainerCore>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() => Task.FromResult((ContainerCore)db.GetContainer("test"))));
            }

            await Task.WhenAll(tasks);

            BatchAsyncContainerExecutor firstExecutor = tasks[0].Result.BatchExecutor;
            Assert.IsNotNull(firstExecutor);
            for (int i = 1; i < 20; i++)
            {
                BatchAsyncContainerExecutor otherExecutor = tasks[i].Result.BatchExecutor;
                Assert.AreEqual(firstExecutor, otherExecutor);
            }
        }

        [TestMethod]
        public void Null_When_OptionsOff()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            CosmosClientContext context = new ClientContextCore(
                client: mockClient.Object,
                clientOptions: new CosmosClientOptions() { },
                userJsonSerializer: null,
                defaultJsonSerializer: null,
                sqlQuerySpecSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: null);

            DatabaseCore db = new DatabaseCore(context, "test");
            ContainerCore container = (ContainerCore)db.GetContainer("test");
            Assert.IsNull(container.BatchExecutor);
        }

        [TestMethod]
        public void Get_And_Dispose()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            CosmosClientContext context = new ClientContextCore(
                client: mockClient.Object,
                clientOptions: new CosmosClientOptions() { AllowBulkExecution = true },
                userJsonSerializer: null,
                defaultJsonSerializer: null,
                sqlQuerySpecSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: null);

            DatabaseCore db = new DatabaseCore(context, "test");
            ContainerCore container = (ContainerCore)db.GetContainer("test");

            BatchAsyncContainerExecutorCache cache = new BatchAsyncContainerExecutorCache();

            BatchAsyncContainerExecutor executor = cache.GetExecutorForContainer(container, context);
            Assert.IsNotNull(executor);

            cache.DisposeExecutor(container);

            // Should return new instance
            BatchAsyncContainerExecutor secondExecutor = cache.GetExecutorForContainer(container, context);
            Assert.AreNotEqual(executor, secondExecutor);
        }

        [TestMethod]
        public async Task DeleteContainerRemovesCache()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            Mock<CosmosClientContext> mockedContext = new Mock<CosmosClientContext>();
            mockedContext.Setup(c => c.Client).Returns(mockClient.Object);
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions() { AllowBulkExecution = true });
            mockedContext.Setup(c => c.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new Uri("/dbs/test/colls/test", UriKind.Relative));
            mockedContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ResponseMessage(System.Net.HttpStatusCode.NoContent)));

            DatabaseCore db = new DatabaseCore(mockedContext.Object, "test");
            ContainerCore container = (ContainerCore)db.GetContainer("test");
            Assert.AreEqual(container.BatchExecutor, mockClient.Object.BatchExecutorCache.GetExecutorForContainer(container, mockedContext.Object));
            await container.DeleteContainerStreamAsync();

            // Asking for a new cache instance should return a new executor as the previous entry should have been deleted
            Assert.AreNotEqual(container.BatchExecutor, mockClient.Object.BatchExecutorCache.GetExecutorForContainer(container, mockedContext.Object));
        }
    }
}
