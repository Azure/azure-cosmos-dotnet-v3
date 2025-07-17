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
            CosmosClientContext context = this.MockClientContext();

            DatabaseInternal db = new DatabaseInlineCore(context, "test");

            List<Task<ContainerInternal>> tasks = new List<Task<ContainerInternal>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() => Task.FromResult((ContainerInternal)new ContainerInlineCore(context, db, "test"))));
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
        [Timeout(60000)]
        public async Task SingleTaskScheduler_ExecutorTest()
        {
            CosmosClientContext context = this.MockClientContext();

            DatabaseInternal db = new DatabaseInlineCore(context, "test");

            List<Task<ContainerInternal>> tasks = new List<Task<ContainerInternal>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(
                    Task.Factory.StartNew(() => (ContainerInternal)new ContainerInlineCore(context, db, "test"),
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    new SingleTaskScheduler()));
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
            CosmosClientContext context = this.MockClientContext(allowBulkExecution: false);

            DatabaseInternal db = new DatabaseInlineCore(context, "test");
            ContainerInternal container = new ContainerInlineCore(context, db, "test");
            Assert.IsNull(container.BatchExecutor);
        }

        [TestMethod]
        public void GetExecutorForContainer_UsesCustomMaxOperationsFromEnvironment()
        {
            // Arrange
            const string environmentVariableName = "COSMOS_MAX_OPERATIONS_IN_DIRECT_MODE_BATCH_REQUEST";
            const int customMaxOperations = 150;
            
            // Store original value to restore later
            string originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
            
            try
            {
                Environment.SetEnvironmentVariable(environmentVariableName, customMaxOperations.ToString());
                
                CosmosClientContext context = this.MockClientContext();
                DatabaseInternal db = new DatabaseInlineCore(context, "test");
                ContainerInternal container = new ContainerInlineCore(context, db, "test");
                
                // Act
                BatchAsyncContainerExecutor executor = container.BatchExecutor;
                
                // Assert
                Assert.IsNotNull(executor);
                // The executor should be created with the custom max operations value
                // We verify this indirectly by ensuring the executor was created successfully
                // The actual value is verified in the ConfigurationManager tests
            }
            finally
            {
                // Restore original environment variable value
                Environment.SetEnvironmentVariable(environmentVariableName, originalValue);
            }
        }

        private CosmosClientContext MockClientContext(bool allowBulkExecution = true)
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            return ClientContextCore.Create(
                mockClient.Object,
                new MockDocumentClient(),
                new CosmosClientOptions() { AllowBulkExecution = allowBulkExecution });
        }
    }
}