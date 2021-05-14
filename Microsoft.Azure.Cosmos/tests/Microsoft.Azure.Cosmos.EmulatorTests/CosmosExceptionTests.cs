//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosUnexpectedExceptionTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInternal)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CheckTracesIncludedWithAllExceptionsTestAsync()
        {
            RequestHandlerHelper requestHandlerHelper = new RequestHandlerHelper();
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                customizeClientBuilder: builder => builder.AddCustomHandlers(requestHandlerHelper));
            Container containerWithFailure = cosmosClient.GetContainer(this.database.Id, this.Container.Id);

            requestHandlerHelper.UpdateRequestMessage = (request) => throw new NullReferenceException("Mock NullReferenceException");
            await this.CheckForTracesAsync<NullReferenceException>(containerWithFailure, messageContainsDiagnostics: true);

            requestHandlerHelper.UpdateRequestMessage = (request) => throw new InvalidOperationException("Mock InvalidOperationException");
            await this.CheckForTracesAsync<InvalidOperationException>(containerWithFailure, messageContainsDiagnostics: true);

            requestHandlerHelper.UpdateRequestMessage = (request) => throw new ObjectDisposedException("Mock ObjectDisposedException");
            await this.CheckForTracesAsync<ObjectDisposedException>(containerWithFailure, messageContainsDiagnostics: false);
        }


        private async Task CheckForTracesAsync<ExceptionType>(
            Container container,
            bool messageContainsDiagnostics) where ExceptionType : Exception
        {
            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();

            try
            {
                await container.CreateItemAsync<ToDoActivity>(
                    toDoActivity,
                    new Cosmos.PartitionKey(toDoActivity.pk));

                Assert.Fail("Should have thrown");
            }
            catch (ExceptionType e)
            {
                if (messageContainsDiagnostics)
                {
                    Assert.IsTrue(e.Message.Contains("Client Configuration"));
                }
                else
                {
                    Assert.IsFalse(e.Message.Contains("CosmosDiagnostics"));
                }

                Assert.IsTrue(e.ToString().Contains("Client Configuration"));
            }
        }
    }
}
