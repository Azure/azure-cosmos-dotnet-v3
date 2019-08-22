//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
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

            PermissionProperties newPermissionProperties = new PermissionProperties(permissionId, PermissionMode.All, containerResponse.Container);
            permissionResponse = await user.GetPermission(permissionId).ReplaceAsync(newPermissionProperties);
            //Backend returns Created instead of OK
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);

            permissionResponse = await user.GetPermission(permissionId).ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, permissionResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);

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
            permission = permissionResponse.Resource;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                ItemResponse<dynamic> response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .DeleteItemAsync<dynamic>(itemId, partitionKey);
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }
    }
}
