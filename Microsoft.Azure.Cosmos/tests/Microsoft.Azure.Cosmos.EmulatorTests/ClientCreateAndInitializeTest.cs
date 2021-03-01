namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientCreateAndInitializeTest : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private const string PartitionKey = "/pk";
        private readonly string DatabaseId = "ClientCreateAndInitializeDatabase" + Guid.NewGuid();
        private readonly string ContainerId = "ClientCreateAndInitializeContainer";

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient(useGateway: false);
            this.database = await this.cosmosClient.CreateDatabaseAsync(
                   id: this.DatabaseId);
            ContainerResponse response = await this.database.CreateContainerAsync(
                        new ContainerProperties(id: this.ContainerId, partitionKeyPath: PartitionKey),
                        throughput: 20000,
                        cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInlineCore)response;

            // Create items with different
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "Status" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateAndInitializeTest()
        {
            int httpCallsMade = 0;
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    httpCallsMade++;
                    return null;
                }
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            List<(string, string)> containers = new List<(string, string)> 
            { (this.DatabaseId, this.ContainerId)};

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper)
            };

            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers, cosmosClientOptions);
            Assert.IsNotNull(cosmosClient);
            int httpCallsMadeAfterCreation = httpCallsMade;

            ContainerInternal container = (ContainerInternal)cosmosClient.GetContainer(this.DatabaseId, this.ContainerId);
            ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>("1", new Cosmos.PartitionKey("Status1"));
            Assert.AreEqual(httpCallsMade, httpCallsMadeAfterCreation);
            cosmosClient.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(HttpRequestException))]
        public async Task AuthIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { (this.DatabaseId, this.ContainerId)};
            string authKey = TestCommon.GetAccountInfo().authKey;
            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync("https://127.0.0.1:0000/", authKey, containers);
            cosmosClient.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task DatabaseIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { ("IncorrectDatabase", this.ContainerId)};
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            try
            {
                CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers);
            }
            catch (CosmosException ex)
            {
                Assert.IsTrue(ex.StatusCode == HttpStatusCode.NotFound);
                throw ex;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task ContainerIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { (this.DatabaseId, "IncorrectContainer")};
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            try
            {
                CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers);
            }
            catch (CosmosException ex)
            {
                Assert.IsTrue(ex.StatusCode == HttpStatusCode.NotFound);
                throw ex;
            }
        }

        [TestMethod]
        public async Task MultipleRequestsTestWithBadExceptionAsync()
        {
            string authKey = TestCommon.GetAccountInfo().authKey;
            CosmosClient cosmosClient = new CosmosClient("https://127.0.0.1:0000/", authKey);
            await this.MultipleTaskClientInitiliazationAsync(cosmosClient);
        }

        [TestMethod]
        public async Task MultipleRequestsTestWithOperationCancelledAsync()
        {
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    throw new OperationCanceledException("TestOperationCancelledException");
                }
            };

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper)
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClient cosmosClient = new CosmosClient(endpoint, authKey, cosmosClientOptions);
            await this.MultipleTaskClientInitiliazationAsync(cosmosClient);
        }

        private async Task MultipleTaskClientInitiliazationAsync(CosmosClient cosmosClient)
        {
            Container container = cosmosClient.GetContainer(this.DatabaseId, this.ContainerId);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity()));
            }

            List<ObjectDisposedException> disposedExceptions = new List<ObjectDisposedException>();
            Exception exception = null;
            foreach (Task task in tasks)
            {
                try
                {
                    await task;
                }
                catch (ObjectDisposedException disposedException)
                {
                    disposedExceptions.Add(disposedException);
                }
                catch (Exception ex)
                {
                    Assert.IsNull(exception, $"Only first request should be CosmosException First:{exception}, {Environment.NewLine}{Environment.NewLine} Second:{ex}");
                    exception = ex;
                }
            }

            // The first request should be actual exception.
            Assert.IsNotNull(exception);
            // Other requests should get object disposed exception
            Assert.AreEqual(tasks.Count - 1, disposedExceptions.Count);

            foreach (ObjectDisposedException objectDisposedException in disposedExceptions)
            {
                Assert.IsTrue(objectDisposedException.Message.Contains(exception.Message), "Object disposed should contain the original error message");
            }
        }
    }
}
