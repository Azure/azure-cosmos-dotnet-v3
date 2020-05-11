//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosUserTests
    {

        private CosmosClient cosmosClient = null;
        private Cosmos.CosmosDatabase cosmosDatabase = null;

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
            string userId = Guid.NewGuid().ToString();

            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(userId, userResponse.Value.Id);
            Assert.IsNotNull(userResponse.Value.ResourceId);

            string newUserId = Guid.NewGuid().ToString();
            userResponse.Value.Id = newUserId;

            userResponse = await this.cosmosDatabase.GetUser(userId).ReplaceAsync(userResponse.Value);
            Assert.AreEqual((int)HttpStatusCode.OK, userResponse.GetRawResponse().Status);
            Assert.AreEqual(newUserId, userResponse.Value.Id);

            userResponse = await this.cosmosDatabase.GetUser(userResponse.Value.Id).ReadAsync();
            Assert.AreEqual((int)HttpStatusCode.OK, userResponse.GetRawResponse().Status);
            Assert.AreEqual(newUserId, userResponse.Value.Id);

            userResponse = await this.cosmosDatabase.GetUser(newUserId).DeleteAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, userResponse.GetRawResponse().Status);

            userId = Guid.NewGuid().ToString();
            userResponse = await this.cosmosDatabase.UpsertUserAsync(userId);
            Assert.AreEqual((int)HttpStatusCode.Created, userResponse.GetRawResponse().Status);
            Assert.AreEqual(userId, userResponse.Value.Id);
            Assert.IsNotNull(userResponse.Value.ResourceId);

            newUserId = Guid.NewGuid().ToString();
            userResponse.Value.Id = newUserId;
            userResponse = await this.cosmosDatabase.UpsertUserAsync(userResponse.Value.Id);
            Assert.AreEqual(newUserId, userResponse.Value.Id);
        }
    }
}
