//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosPermissionTests
    {
        private CosmosClient cosmosClient = null;
        private Database cosmosDatabase = null;
        private string databaseResourceId = null;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
            databaseResourceId = cosmosDatabaseResponse.Resource.ResourceId;
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
        public async Task StreamCRUDTest()
        {
            string containerId = Guid.NewGuid().ToString();
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerId, "/id");
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            string userId = Guid.NewGuid().ToString();

            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);            

            string permissionId = Guid.NewGuid().ToString();
            PermissionProperties permissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.Read, containerResponse.Container);

            User user = userResponse.User;
            CosmosJsonDotNetSerializer defaultJsonSerializer = new CosmosJsonDotNetSerializer();

            ResponseMessage responseMessage = await user.CreatePermissionStreamAsync(permissionProperties);
            PermissionProperties permission = defaultJsonSerializer.FromStream<PermissionProperties>(responseMessage.Content);
            Assert.AreEqual(HttpStatusCode.Created, responseMessage.StatusCode);
            Assert.AreEqual(permissionId, permission.Id);
            Assert.AreEqual(permissionProperties.PermissionMode, permission.PermissionMode);

            PermissionProperties newPermissionProperties = PermissionProperties.CreateForContainer(permissionId, PermissionMode.All, containerResponse.Container);

            responseMessage = await user.GetPermission(permissionId).ReplacePermissionStreamAsync(newPermissionProperties);
            //Backend returns Created instead of OK
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            permission = defaultJsonSerializer.FromStream<PermissionProperties>(responseMessage.Content);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permission.PermissionMode);

            responseMessage = await user.GetPermission(permissionId).ReadPermissionStreamAsync();
            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
            permission = defaultJsonSerializer.FromStream<PermissionProperties>(responseMessage.Content);
            Assert.AreEqual(newPermissionProperties.Id, permission.Id);
            Assert.AreEqual(newPermissionProperties.PermissionMode, permission.PermissionMode);

            responseMessage = await user.GetPermission(permissionId).DeletePermissionStreamAsync();
            //Backend returns Created instead of NoContent
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);

            responseMessage = await user.GetPermission(permissionId).ReadPermissionStreamAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, responseMessage.StatusCode);
        }
    }
}
