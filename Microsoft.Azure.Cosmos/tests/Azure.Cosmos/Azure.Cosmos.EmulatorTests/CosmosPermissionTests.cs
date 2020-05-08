//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosPermissionTests
    {
        private CosmosClient cosmosClient = null;
        private CosmosDatabase cosmosDatabase = null;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            CosmosDatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
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
            CosmosContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);

            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            CosmosUser user = userResponse.User;
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(userId, user.Id);

            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, containerResponse.Container);
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permissionResponse.Value.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permissionResponse.Value.PermissionMode);
            Assert.IsNotNull(permissionResponse.Value.Token);

            PermissionProperties newPermissionProperties = new PermissionProperties(permissionId, PermissionMode.All, containerResponse.Container);
            permissionResponse = await user.GetPermission(permissionId).ReplaceAsync(newPermissionProperties);
            //Backend returns Created instead of OK
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permissionResponse.Value.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permissionResponse.Value.PermissionMode);

            permissionResponse = await user.GetPermission(permissionId).ReadAsync();
            Assert.AreEqual((int)HttpStatusCode.OK, permissionResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permissionResponse.Value.Id);

            permissionResponse = await user.GetPermission(permissionId).DeleteAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, permissionResponse.GetRawResponse().Status);

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
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permissionResponse.Value.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permissionResponse.Value.PermissionMode);
            Assert.IsNotNull(permissionResponse.Value.Token);

            newPermissionProperties = new PermissionProperties(permissionId, PermissionMode.All, containerResponse.Container);
            permissionResponse = await user.UpsertPermissionAsync(newPermissionProperties);
            Assert.AreEqual((int)HttpStatusCode.OK, permissionResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permissionResponse.Value.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permissionResponse.Value.PermissionMode);
        }

        [TestMethod]
        public async Task ContainerResourcePermissionTest()
        {
            //create user
            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(userId, userResponse.Value.Id);
            CosmosUser user = userResponse.User;

            //create resource
            string containerId = Guid.NewGuid().ToString();
            CosmosContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            CosmosContainer container = containerResponse.Container;
            
            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, container);
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            PermissionProperties permission = permissionResponse.Value;
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permission.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permission.PermissionMode);

            //delete resource with PermissionMode.Read
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(options: null, resourceToken: permission.Token))
            {
                try
                {
                    CosmosContainerResponse response = await tokenCosmosClient
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
            permission = permissionResponse.Value;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(options: null, resourceToken: permission.Token))
            {
                CosmosContainerResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .DeleteContainerAsync();
                Assert.AreEqual((int)HttpStatusCode.NoContent, response.GetRawResponse().Status);
            }
        }

        [TestMethod]
        public async Task ItemResourcePermissionTest()
        {
            //create user
            string userId = Guid.NewGuid().ToString();
            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(userId, userResponse.Value.Id);
            CosmosUser user = userResponse.User;

            //create resource
            string containerId = Guid.NewGuid().ToString();
            CosmosContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            CosmosContainer container = containerResponse.Container;
            string itemId = Guid.NewGuid().ToString();
            PartitionKey partitionKey = new PartitionKey(itemId);
            ItemResponse<dynamic> itemRespnose = await container.CreateItemAsync<dynamic>(new { id = itemId }, partitionKey);
            Assert.AreEqual((int)HttpStatusCode.Created, itemRespnose.GetRawResponse().Status);

            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = new PermissionProperties(permissionId, PermissionMode.Read, container, partitionKey, itemId);
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            PermissionProperties permission = permissionResponse.Value;
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(permissionId, permission.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permission.PermissionMode);

            //delete resource with PermissionMode.Read
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(options: null, resourceToken: permission.Token))
            {
                try
                {
                    ItemResponse<dynamic> response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .DeleteItemAsync<dynamic>(itemId, partitionKey);
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
            permission = permissionResponse.Value;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(options: null, resourceToken: permission.Token))
            {
                ItemResponse<dynamic> response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .DeleteItemAsync<dynamic>(itemId, partitionKey);
                Assert.AreEqual((int)HttpStatusCode.NoContent, response.GetRawResponse().Status);
            }
        }

        [TestMethod]
        public async Task EnsureUnauthorized_ThrowsCosmosClientException()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            // Take the key and change some middle character
            authKey = authKey.Replace("m", "M");

            CosmosClient cosmosClient = new CosmosClient(
                endpoint,
                authKey);

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => cosmosClient.GetContainer("test", "test").ReadItemAsync<dynamic>("test", new PartitionKey("test")));
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }
    }
}
