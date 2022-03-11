//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class InternalFriendsTest
    {
        private CosmosClient cosmosClient;

        [TestCleanup]
        public void Cleanup()
        {
            this.cosmosClient?.Dispose();
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ClientEventualWriteStrongReadConsistencyEnabledTestAsync(bool isStrongReadWithEventualConsistencyAccount)
        {
            Container container = await this.CreateContainer(isStrongReadWithEventualConsistencyAccount);

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(item: testItem);

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            ItemRequestOptions requestOptions = new();
            requestOptions.ConsistencyLevel = Cosmos.ConsistencyLevel.Strong;
            
            if (isStrongReadWithEventualConsistencyAccount)
            {
                ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk), requestOptions);
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            } else
            {
                Assert.ThrowsException<ArgumentException>(async () => await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk), requestOptions));
            }
        }

        private async Task<Container> CreateContainer(bool isStrongReadWithEventualConsistencyAccount)
        {
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

            RequestHandlerHelper handlerHelper = new RequestHandlerHelper
            {
                UpdateRequestMessage = (request) =>
                {
                    if (request.OperationType == Documents.OperationType.Read)
                    {
                        Assert.AreEqual(Cosmos.ConsistencyLevel.Strong.ToString(), request.Headers[Documents.HttpConstants.HttpHeaders.ConsistencyLevel]);
                    }
                }
            };

            this.cosmosClient = TestCommon.CreateCosmosClient(x =>
            {
                CosmosClientBuilder builder = x.AddCustomHandlers(handlerHelper)
                                               .WithHttpClientFactory(() => new HttpClient(httpHandler));
                if (isStrongReadWithEventualConsistencyAccount)
                {
                    builder.WithStrongReadWithEventualConsistencyAccount();
                }
            });

            Database database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: new CancellationTokenSource().Token);

            return await database.CreateContainerAsync(id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/pk");
        }
    }
}
