//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

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

            string newUserId = Guid.NewGuid().ToString();
            userResponse.Resource.Id = newUserId;

            userResponse = await this.cosmosDatabase.GetUser(userId).ReplaceAsync(userResponse.Resource);
            Assert.AreEqual(HttpStatusCode.OK, userResponse.StatusCode);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);

            userResponse = await this.cosmosDatabase.GetUser(userResponse.Resource.Id).ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, userResponse.StatusCode);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);

            userResponse = await this.cosmosDatabase.GetUser(newUserId).DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, userResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamCRUDTest()
        {
            string userId = Guid.NewGuid().ToString();


            ResponseMessage responseMessage = await this.cosmosDatabase.CreateUserStreamAsync(new UserProperties(userId));
            Assert.AreEqual(HttpStatusCode.Created, responseMessage.StatusCode);
            UserProperties user = TestCommon.Serializer.FromStream<UserProperties>(responseMessage.Content);
            Assert.AreEqual(userId, user.Id);
            Assert.IsNotNull(user.ResourceId);

            string newUserId = Guid.NewGuid().ToString();
            user.Id = newUserId;

            responseMessage = await this.cosmosDatabase.GetUser(userId).ReplaceStreamAsync(user);
            user = TestCommon.Serializer.FromStream<UserProperties>(responseMessage.Content);
            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
            Assert.AreEqual(newUserId, user.Id);

            responseMessage = await this.cosmosDatabase.GetUser(newUserId).ReadStreamAsync();
            user = TestCommon.Serializer.FromStream<UserProperties>(responseMessage.Content);
            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
            Assert.AreEqual(newUserId, user.Id);

            responseMessage = await this.cosmosDatabase.GetUser(newUserId).DeleteStreamAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, responseMessage.StatusCode);
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            string userId = Guid.NewGuid().ToString();

            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);

            HashSet<string> userIds = new HashSet<string>();
            FeedIterator<UserProperties> resultSet = this.cosmosDatabase.GetUserQueryIterator<UserProperties>();
            while (resultSet.HasMoreResults)
            {
                foreach (UserProperties userProperties in await resultSet.ReadNextAsync())
                {
                    if (!userIds.Contains(userProperties.Id))
                    {
                        userIds.Add(userProperties.Id);
                    }
                }
            }

            Assert.IsTrue(userIds.Count > 0);
            Assert.IsTrue(userIds.Contains(userId));
        }

        [TestMethod]
        public async Task StreamIteratorTest()
        {
            string userId = Guid.NewGuid().ToString();

            UserResponse userResponse = await this.cosmosDatabase.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);

            HashSet<string> userIds = new HashSet<string>();
            FeedIterator resultSet = this.cosmosDatabase.GetUserQueryStreamIterator(
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });

            while (resultSet.HasMoreResults)
            {
                using (ResponseMessage message = await resultSet.ReadNextAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                    dynamic users = TestCommon.Serializer.FromStream<dynamic>(message.Content).Users;
                    foreach (dynamic user in users)
                    {
                        string id = user.id.ToString();
                        userIds.Add(id);
                    }
                }
            }

            Assert.IsTrue(userIds.Count > 0);
            Assert.IsTrue(userIds.Contains(userId));
        }
    }
}
