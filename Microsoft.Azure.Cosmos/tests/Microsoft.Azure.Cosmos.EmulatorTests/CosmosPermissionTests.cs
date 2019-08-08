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
            PermissionProperties permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.Read, containerResponse.Container);
            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);
            Assert.IsNotNull(permissionResponse.Resource.Token);

            PermissionProperties newPermissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, containerResponse.Container);
            permissionResponse = await user.GetPermission(permissionId).ReplacePermissionAsync(newPermissionProperties);
            //Backend returns Created instead of OK
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permissionResponse.Resource.PermissionMode);

            permissionResponse = await user.GetPermission(permissionId).ReadPermissionAsync();
            Assert.AreEqual(HttpStatusCode.OK, permissionResponse.StatusCode);
            Assert.AreEqual(permissionId, permissionResponse.Resource.Id);

            permissionResponse = await user.GetPermission(permissionId).DeletePermissionAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, permissionResponse.StatusCode);

            try
            {
                permissionResponse = await user.GetPermission(permissionId).ReadPermissionAsync();
                Assert.Fail();
            }
            catch(CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
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
            PermissionProperties permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.Read, container);
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
            permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplacePermissionAsync(permissionProperties);
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
            PermissionProperties permissionProperties = PermissionProperties.CreateForItem(permissionId, PermissionMode.Read, container, partitionKey, itemId);
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
            permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplacePermissionAsync(permissionProperties);
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

        [TestMethod]
        public async Task StoredProcedureResourcePermissionTest()
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
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = "function() { { var x = 42; } }";
            StoredProcedureResponse storedProcedureResponse = await container.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);

            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = PermissionProperties.CreateForStoredProcedure(permissionId, PermissionMode.Read, container, sprocId);
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
                    StoredProcedureResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .Scripts
                    .DeleteStoredProcedureAsync(sprocId);
                    Assert.Fail();
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
                }
            }

            //update permission to PermissionMode.All
            permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplacePermissionAsync(permissionProperties);
            permission = permissionResponse.Resource;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                StoredProcedureResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .Scripts
                    .DeleteStoredProcedureAsync(sprocId);
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }

        [TestMethod]
        public async Task UserDefinedFunctionResourcePermissionTest()
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
            string udfId = Guid.NewGuid().ToString();
            string udfBody = @"function(amt) { return amt * 0.05; }";
            UserDefinedFunctionResponse userDefinedFunctionResponse =
                await container.Scripts.CreateUserDefinedFunctionAsync(
                    new UserDefinedFunctionProperties
                    {
                        Id = udfId,
                        Body = udfBody
                    });
            Assert.AreEqual(HttpStatusCode.Created, userDefinedFunctionResponse.StatusCode);

            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = PermissionProperties.CreateForUserDefinedFunction(permissionId, PermissionMode.Read, container, udfId);
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
                    UserDefinedFunctionResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .Scripts
                    .DeleteUserDefinedFunctionAsync(udfId);
                    Assert.Fail();
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
                }
            }

            //update permission to PermissionMode.All
            permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplacePermissionAsync(permissionProperties);
            permission = permissionResponse.Resource;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                UserDefinedFunctionResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .Scripts
                    .DeleteUserDefinedFunctionAsync(udfId);
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }

        [TestMethod]
        public async Task TriggerResourcePermissionTest()
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
            string triggerId = Guid.NewGuid().ToString();
            TriggerResponse triggerResponse =
                await container.Scripts.CreateTriggerAsync(
                    new TriggerProperties
                    {
                        Id = triggerId,
                        Body = "function Test(){ }",
                        TriggerOperation = TriggerOperation.All,
                        TriggerType = TriggerType.Pre
                    });
            Assert.AreEqual(HttpStatusCode.Created, triggerResponse.StatusCode);

            //create permission
            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = PermissionProperties.CreateForTrigger(permissionId, PermissionMode.Read, container, triggerId);
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
                    TriggerResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .Scripts
                    .DeleteTriggerAsync(triggerId);
                    Assert.Fail();
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
                }
            }

            //update permission to PermissionMode.All
            permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, container);
            permissionResponse = await user.GetPermission(permissionId).ReplacePermissionAsync(permissionProperties);
            permission = permissionResponse.Resource;

            //delete resource with PermissionMode.All
            using (CosmosClient tokenCosmosClient = TestCommon.CreateCosmosClient(clientOptions: null, resourceToken: permission.Token))
            {
                TriggerResponse response = await tokenCosmosClient
                    .GetDatabase(this.cosmosDatabase.Id)
                    .GetContainer(containerId)
                    .Scripts
                    .DeleteTriggerAsync(triggerId);
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }
    }
}
