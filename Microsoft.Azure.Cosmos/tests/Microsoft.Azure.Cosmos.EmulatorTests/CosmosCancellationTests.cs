//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosCancellationTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
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
        public async Task CheckCancellationTokenGatewayTestAsync()
        {
            using (CosmosClient gatewayClient = TestCommon.CreateCosmosClient(
                builder => builder.WithConnectionModeGateway()))
            {
                Container gatewayContainer = gatewayClient.GetContainer(this.database.Id, this.Container.Id);
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.Cancel();
                await this.CheckCancellationTokenTestAsync(gatewayContainer, cancellationTokenSource.Token);
            }
        }

        [TestMethod]
        public async Task CheckCancellationWithTransportIntercepterTestAsync()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Container withCancellationToken = TransportClientHelper.GetContainerWithIntercepter(
                 this.database.Id,
                 this.Container.Id,
                 (uri, resourceOperation, documentServiceRequest) =>
                 {
                     if (documentServiceRequest.ResourceType == Documents.ResourceType.Document)
                     {
                         cancellationTokenSource.Cancel();
                         cancellationTokenSource.Token.ThrowIfCancellationRequested();
                     }
                 });

            await this.CheckCancellationTokenTestAsync(withCancellationToken, cancellationTokenSource.Token);
        }

        [TestMethod]
        public async Task CheckCancellationTokenDirectTestAsync()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            await this.CheckCancellationTokenTestAsync(this.Container, cancellationTokenSource.Token);
        }


        private async Task CheckCancellationTokenTestAsync(
            Container container,
            CancellationToken cancellationToken)
        {
            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();

            try
            {
                await container.CreateItemAsync<ToDoActivity>(
                    toDoActivity,
                    new Cosmos.PartitionKey(toDoActivity.status),
                    cancellationToken: cancellationToken);

                Assert.Fail("Should have thrown");
            }
            catch (CosmosOperationCanceledException ce)
            {
                Assert.IsNotNull(ce);
                string message = ce.Message;
                string diagnostics = ce.Diagnostics.ToString();
                string toString = ce.ToString();
                Assert.IsTrue(toString.Contains(diagnostics));
                Assert.IsTrue(toString.Contains(message));
            }

            try
            {
                FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                    "select * from T");

                await feedIterator.ReadNextAsync(cancellationToken: cancellationToken);

                Assert.Fail("Should have thrown");
            }
            catch (CosmosOperationCanceledException ce)
            {
                Assert.IsNotNull(ce);
                string message = ce.Message;
                string diagnostics = ce.Diagnostics.ToString();
                string toString = ce.ToString();
                Assert.IsTrue(toString.Contains(diagnostics));
                Assert.IsTrue(toString.Contains(message));
            }
        }
    }
}
