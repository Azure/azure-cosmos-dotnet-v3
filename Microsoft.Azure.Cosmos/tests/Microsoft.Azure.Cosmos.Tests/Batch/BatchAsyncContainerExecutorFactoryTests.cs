//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]    
    public class BatchAsyncContainerExecutorFactoryTests
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

            BatchAsyncContainerExecutor executor = BatchAsyncContainerExecutorFactory.GetExecutorForContainer(container, context);
            Assert.IsNotNull(executor);

            BatchAsyncContainerExecutorFactory.DisposeExecutor(container);

            // Should return new instance
            BatchAsyncContainerExecutor secondExecutor = BatchAsyncContainerExecutorFactory.GetExecutorForContainer(container, context);
            Assert.AreNotEqual(executor, secondExecutor);
        }
    }
}
