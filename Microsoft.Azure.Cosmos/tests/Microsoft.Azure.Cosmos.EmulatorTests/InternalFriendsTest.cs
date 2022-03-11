//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class InternalFriendsTest : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ClientEventualWriteStrongReadConsistencyEnabledTestAsync()
        {
            // Making sure account level consistency is always eventual
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                ResponseIntercepter = async (response) =>
                {
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (responseString.Contains("databaseAccountEndpoint"))
                    {
                        AccountProperties accountProperties =
                                    JsonConvert.DeserializeObject<AccountProperties>(responseString);
                        accountProperties.Consistency.DefaultConsistencyLevel = Cosmos.ConsistencyLevel.Eventual;
                        response.Content = new StringContent(JsonConvert.SerializeObject(accountProperties), Encoding.UTF8, "application/json");
                    }
                    return response;
                }
            };

            RequestHandlerHelper handlerHelper = new RequestHandlerHelper();
            using CosmosClient cosmosClient = TestCommon.CreateCosmosClient(x =>
                x.AddCustomHandlers(handlerHelper)
                .WithHttpClientFactory(() => new HttpClient(httpHandler))
                .WithStrongReadWithEventualConsistencyAccount());

            Container consistencyContainer = cosmosClient.GetContainer(this.database.Id, this.Container.Id);

            int requestCount = 0;
            handlerHelper.UpdateRequestMessage = (request) =>
            {
                if (request.OperationType == Documents.OperationType.Read)
                {
                    Assert.AreEqual(Cosmos.ConsistencyLevel.Strong.ToString(), request.Headers[Documents.HttpConstants.HttpHeaders.ConsistencyLevel]);
                }

                requestCount++;
            };

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await consistencyContainer.CreateItemAsync<ToDoActivity>(item: testItem);

            ItemRequestOptions requestOptions = new();
            requestOptions.ConsistencyLevel = Cosmos.ConsistencyLevel.Strong;

            response = await consistencyContainer.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk), requestOptions);

            Assert.AreEqual(2, requestCount);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task ClientEventualWriteStrongReadConsistencyDisabledTestAsync()
        {
            // Making sure account level consistency is always eventual
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                ResponseIntercepter = async (response) =>
                {
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (responseString.Contains("databaseAccountEndpoint"))
                    {
                        AccountProperties accountProperties =
                                    JsonConvert.DeserializeObject<AccountProperties>(responseString);
                        accountProperties.Consistency.DefaultConsistencyLevel = Cosmos.ConsistencyLevel.Eventual;
                        response.Content = new StringContent(JsonConvert.SerializeObject(accountProperties), Encoding.UTF8, "application/json");
                    }
                    return response;
                }
            };

            RequestHandlerHelper handlerHelper = new RequestHandlerHelper();
            using CosmosClient cosmosClient = TestCommon.CreateCosmosClient(x =>
                x.AddCustomHandlers(handlerHelper)
                .WithHttpClientFactory(() => new HttpClient(httpHandler)));

            Container consistencyContainer = cosmosClient.GetContainer(this.database.Id, this.Container.Id);

            int requestCount = 0;
            handlerHelper.UpdateRequestMessage = (request) =>
            {
                if (request.OperationType == Documents.OperationType.Read)
                {
                    Assert.AreEqual(Cosmos.ConsistencyLevel.Strong.ToString(), request.Headers[Documents.HttpConstants.HttpHeaders.ConsistencyLevel]);
                }

                requestCount++;
            };

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await consistencyContainer.CreateItemAsync<ToDoActivity>(item: testItem);

            ItemRequestOptions requestOptions = new();
            requestOptions.ConsistencyLevel = Cosmos.ConsistencyLevel.Strong;

            response = await consistencyContainer.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk), requestOptions);

            Assert.AreEqual(2, requestCount);
        }
    }
}
