//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosClientContentResponseTests
    {
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;
        private ContainerInternal containerInternal;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = this.CreateCosmosClientWithContentResponse();
            this.database = await this.cosmosClient.CreateDatabaseAsync(
                   id: Guid.NewGuid().ToString());

            this.container = await this.database.CreateContainerAsync(
                     id: "ClientItemNoResponseTest",
                     partitionKeyPath: "/status");
            this.containerInternal = (ContainerInternal)this.container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            using (await this.database.DeleteStreamAsync()) { }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task ClientContentResponseTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNotNull(itemResponse.Resource);
        }

        [TestMethod]
        public async Task ClientContentResponseWithItemRequestOverrideTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNull(itemResponse.Resource);
        }

        [TestMethod]
        public async Task ClientContentResponseWithItemRequestOverrideTrueTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = true
            };

            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNotNull(itemResponse.Resource);
        }

        private CosmosClient CreateCosmosClientWithContentResponse()
        {
            CosmosClientBuilder cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
            cosmosClientBuilder = cosmosClientBuilder.WithContentResponseOnWriteEnabled(true);
            return cosmosClientBuilder.Build();
        }
    }
}
