//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.EmulatorTests.Query;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosPermissionTests
    {
        private CosmosClient cosmosClient = null;
        private Database cosmosDatabase = null;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.cosmosDatabase != null)
            {
                await this.cosmosDatabase.DeleteStreamAsync();
            }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task CRUDTest()
        {
            string containerId = Guid.NewGuid().ToString();
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            User user = userResponse.User;
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, user.Id);

            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, containerResponse.Container);
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);
            Assert.IsNotNull(permissionResponse.Resource.Token);
            SelflinkValidator.ValidatePermissionSelfLink(permissionResponse.Resource.SelfLink);

            PermissionProperties newPermissionProperties = new PermissionProperties(permissionId, PermissionMode.All, containerResponse.Container);
            permissionResponse = await user.GetPermission(permissionId).ReplaceAsync(newPermissionProperties);
            //Backend returns Created instead of OK
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);
            SelflinkValidator.ValidatePermissionSelfLink(permissionResponse.Resource.SelfLink);

            permissionResponse = await user.GetPermission(permissionId).ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, permissionResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            SelflinkValidator.ValidatePermissionSelfLink(permissionResponse.Resource.SelfLink);

            permissionResponse = await user.GetPermission(permissionId).DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, permissionResponse.StatusCode);

            try
            {
                permissionResponse = await user.GetPermission(permissionId).ReadAsync();
                Assert.Fail();
            }
            catch(CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }

            permissionId = Guid.NewGuid().ToString();
            permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, containerResponse.Container);
            permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);
            Assert.IsNotNull(permissionResponse.Resource.Token);

            newPermissionProperties = new PermissionProperties(permissionId, PermissionMode.All, containerResponse.Container);
            permissionResponse = await user.UpsertPermissionAsync(newPermissionProperties);
            Assert.AreEqual(HttpStatusCode.OK, permissionResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);
        }

        [TestMethod]
        public async Task ContainerResourcePermissionTest()
        {
            //create user
            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            User user = userResponse.User;

            //create resource
            string containerId = Guid.NewGuid().ToString();
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse.Container;
            
            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, container);
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            PermissionProperties permission = permissionResponse.Resource;
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permission.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permission.PermissionMode);

            //delete resource with PermissionMode.Read
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                try
                {
                    ContainerResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .DeleteContainerAsync();
                    Assert.Fail();
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
                }
            }
           
            //update permission to PermissionMode.All
            permissionProperties = new PermissionProperties(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplaceAsync(permissionProperties);
            permission = permissionResponse.Resource;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                ContainerResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .DeleteContainerAsync();
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }

        [TestMethod]
        [DataRow(ConnectionMode.Gateway)]
        [DataRow(ConnectionMode.Direct)]
        public async Task ContainerPartitionResourcePermissionTest(ConnectionMode connectionMode)
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(cosmosClientOptions);

            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync("PermissionTest");

            //create user
            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await database.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            User user = userResponse.User;

            //create resource
            string containerId = Guid.NewGuid().ToString();

            ContainerResponse containerResponse = await database.CreateContainerAsync(
                id: containerId,
                partitionKeyPath: "/id");

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse.Container;

            // Create items to read
            ToDoActivity itemAccess = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity itemNoAccess = ToDoActivity.CreateRandomToDoActivity();

            await container.CreateItemAsync<ToDoActivity>(
                itemAccess,
                new PartitionKey(itemAccess.id));

            await container.CreateItemAsync<ToDoActivity>(
                itemNoAccess,
                new PartitionKey(itemNoAccess.id));

            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PartitionKey partitionKey = new PartitionKey(itemAccess.id);
            PermissionProperties permissionProperties = new PermissionProperties(
                permissionId,
                PermissionMode.Read,
                container,
                partitionKey);

            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            PermissionProperties permission = permissionResponse.Resource;
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permission.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permission.PermissionMode);

            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: cosmosClientOptions, resourceToken: permission.Token))
            {
                Container tokenContainer = tokenCosmosClient.GetContainer(database.Id, containerId);
                await tokenContainer.ReadItemAsync<ToDoActivity>(itemAccess.id, new PartitionKey(itemAccess.id));

                try
                {
                    await tokenContainer.ReadItemAsync<ToDoActivity>(itemNoAccess.id, new PartitionKey(itemNoAccess.id));
                    Assert.Fail();
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
                }

                QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(itemAccess.id)
                };

                FeedIterator<ToDoActivity> feedIterator = tokenContainer.GetItemQueryIterator<ToDoActivity>(
                    queryText: "select * from T",
                    requestOptions: queryRequestOptions);

                List<ToDoActivity> result = new List<ToDoActivity>();
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> toDoActivities = await feedIterator.ReadNextAsync();
                    result.AddRange(toDoActivities);
                }

                Assert.AreEqual(1, result.Count);

                // Test query with no service interop via gateway query plan to replicate x32 app
                ContainerInternal containerCore = (ContainerInlineCore)tokenContainer;
                MockCosmosQueryClient mock = new MockCosmosQueryClient(
                    clientContext: containerCore.ClientContext,
                    cosmosContainerCore: containerCore,
                    forceQueryPlanGatewayElseServiceInterop: true);

                Container tokenGatewayQueryPlan = new ContainerInlineCore(
                    containerCore.ClientContext,
                    (DatabaseInternal)containerCore.Database,
                    containerCore.Id,
                    mock);

                FeedIterator<ToDoActivity> feedIteratorGateway = tokenGatewayQueryPlan.GetItemQueryIterator<ToDoActivity>(
                    queryText: "select * from T",
                    requestOptions: queryRequestOptions);

                List<ToDoActivity> resultGateway = new List<ToDoActivity>();
                while (feedIteratorGateway.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> toDoActivities = await feedIteratorGateway.ReadNextAsync();
                    resultGateway.AddRange(toDoActivities);
                }

                Assert.AreEqual(1, resultGateway.Count);
            }
        }

        [TestMethod]
        public async Task ItemResourcePermissionTest()
        {
            //create user
            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            User user = userResponse.User;

            //create resource
            string containerId = Guid.NewGuid().ToString();
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse.Container;
            string itemId = Guid.NewGuid().ToString();
            PartitionKey partitionKey = new PartitionKey(itemId);
            ItemResponse<dynamic> itemRespnose = await container.CreateItemAsync<dynamic>(new { id = itemId }, partitionKey);
            Assert.AreEqual(HttpStatusCode.Created, itemRespnose.StatusCode);

            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, container, partitionKey, itemId);
            Assert.AreEqual(permissionProperties.ResourceUri, $"dbs/{container.Database.Id}/colls/{container.Id}/docs/{itemId}");
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            PermissionProperties permission = permissionResponse.Resource;
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permission.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permission.PermissionMode);

            //delete resource with PermissionMode.Read
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                Container tokenContainer = tokenCosmosClient.GetContainer(this.cosmosDatabase.Id, containerId);
                ItemResponse<dynamic> readPermissionItem = await tokenContainer.ReadItemAsync<dynamic>(itemId, partitionKey);
                Assert.AreEqual(itemId, readPermissionItem.Resource.id.ToString());

                try
                {
                    ItemResponse<dynamic> response = await tokenContainer.DeleteItemAsync<dynamic>(
                        itemId,
                        partitionKey);

                    Assert.Fail();
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
                }
            }

            //update permission to PermissionMode.All
            permissionProperties = new PermissionProperties(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplaceAsync(permissionProperties);
            permission = permissionResponse.Resource;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                using (FeedIterator<dynamic> feed = tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .GetItemQueryIterator<dynamic>(new QueryDefinition("select * from t")))
                {
                    while (feed.HasMoreResults)
                    {
                        FeedResponse<dynamic> response = await feed.ReadNextAsync();
                        Assert.IsNotNull(response);
                    }
                }
            }
        }

        [TestMethod]
        public async Task EnsureUnauthorized_ThrowsCosmosClientException()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            // Take the key and change some middle character
            authKey = authKey.Replace("m", "M");

            using CosmosClient cosmosClient = new CosmosClient(
                endpoint,
                authKey);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => cosmosClient.GetContainer("test", "test").ReadItemAsync<dynamic>("test", new PartitionKey("test")));
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [TestMethod]
        public async Task EnsureUnauthorized_Writes_ThrowsCosmosClientException()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            
            // Take the key and change some middle character
            authKey = authKey.Replace("m", "M");

            using CosmosClient cosmosClient = new CosmosClient(
                endpoint,
                authKey);
            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => cosmosClient.GetContainer("test", "test").CreateItemAsync<dynamic>(new { id = "test" }));
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [TestMethod]
        public async Task EnsureUnauthorized_Query_ThrowsCosmosClientException()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            
            // Take the key and change some middle character
            authKey = authKey.Replace("m", "M");

            using CosmosClient cosmosClient = new CosmosClient(
                endpoint,
                authKey);

            using FeedIterator<dynamic> iterator = cosmosClient.GetContainer("test", "test").GetItemQueryIterator<dynamic>("SELECT * FROM c");

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => iterator.ReadNextAsync());
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }
    }
}
