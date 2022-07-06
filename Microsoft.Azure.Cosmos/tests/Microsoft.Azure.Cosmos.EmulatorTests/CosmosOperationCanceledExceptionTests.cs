﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosOperationCanceledExceptionTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
           OpenTelemetryTests.ClassInitialize(context);
        }

        [ClassCleanup]
        public static void FinalCleanup()
        {
           OpenTelemetryTests.FinalCleanup();
        }

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
                     }
                 },
                 useGatewayMode: false,
                 (uri, resourceOperation, documentServiceRequest) 
                    => TransportClientHelper.ReturnThrottledStoreResponseOnItemOperation(uri, resourceOperation, documentServiceRequest, Guid.NewGuid(), string.Empty));

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
                    new Cosmos.PartitionKey(toDoActivity.pk),
                    cancellationToken: cancellationToken);

                Assert.Fail("Should have thrown");
            }
            catch (CosmosOperationCanceledException ce)
            {
                Assert.IsNotNull(ce);
                string message = ce.Message;
                string diagnostics = ce.Diagnostics.ToString();
                string toString = ce.ToString();
                Assert.IsTrue(toString.Contains(diagnostics), $"Exception ToString() : {toString} does not contain diagnostics: {diagnostics}");
                Assert.IsTrue(message.Contains(diagnostics), $"Message : {message} does not contain diagnostics: {diagnostics}");
                string messageWithoutDiagnostics = message[..message.IndexOf(Environment.NewLine)].Trim();
                Assert.IsTrue(toString.Contains(messageWithoutDiagnostics), $"Exception ToString() : {toString} does not contain message: {messageWithoutDiagnostics}");
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
                string message = ce.Message;
                string diagnostics = ce.Diagnostics.ToString();
                string toString = ce.ToString();
                Assert.IsTrue(toString.Contains(diagnostics), $"Exception ToString() : {toString} does not contain diagnostics: {diagnostics}");
                Assert.IsTrue(message.Contains(diagnostics), $"Message : {message} does not contain diagnostics: {diagnostics}");
                string messageWithoutDiagnostics = message[..message.IndexOf(Environment.NewLine)].Trim();
                Assert.IsTrue(toString.Contains(messageWithoutDiagnostics), $"Exception ToString() : {toString} does not contain message: {messageWithoutDiagnostics}");
            }
        }
    }
}
