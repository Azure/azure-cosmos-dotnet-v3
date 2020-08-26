//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosUserTests
    {

        private CosmosClient cosmosClient = null;
        private Cosmos.Database cosmosDatabase = null;

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
            string userId = Guid.NewGuid().ToString();

            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            Assert.IsNotNull(userResponse.Resource.ResourceId);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            string newUserId = Guid.NewGuid().ToString();
            userResponse.Resource.Id = newUserId;

            userResponse = await this.cosmosDatabase.GetUser(userId).ReplaceAsync(userResponse.Resource);
            Assert.AreEqual(HttpStatusCode.OK, userResponse.StatusCode);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            userResponse = await this.cosmosDatabase.GetUser(userResponse.Resource.Id).ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, userResponse.StatusCode);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            userResponse = await this.cosmosDatabase.GetUser(newUserId).DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, userResponse.StatusCode);

            userId = Guid.NewGuid().ToString();
            userResponse = await this.cosmosDatabase.UpsertUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            Assert.IsNotNull(userResponse.Resource.ResourceId);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            newUserId = Guid.NewGuid().ToString();
            userResponse.Resource.Id = newUserId;
            userResponse = await this.cosmosDatabase.UpsertUserAsync(userResponse.Resource.Id);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);
        }
    }
}
