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
    public class CosmosUserTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CRUDTest()
        {
            string userId = Guid.NewGuid().ToString();

            UserResponse userResponse = await this.database.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            Assert.IsNotNull(userResponse.Resource.ResourceId);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            string newUserId = Guid.NewGuid().ToString();
            userResponse.Resource.Id = newUserId;

            userResponse = await this.database.GetUser(userId).ReplaceAsync(userResponse.Resource);
            Assert.AreEqual(HttpStatusCode.OK, userResponse.StatusCode);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            userResponse = await this.database.GetUser(userResponse.Resource.Id).ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, userResponse.StatusCode);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            userResponse = await this.database.GetUser(newUserId).DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, userResponse.StatusCode);

            userId = Guid.NewGuid().ToString();
            userResponse = await this.database.UpsertUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            Assert.AreEqual(userId, userResponse.Resource.Id);
            Assert.IsNotNull(userResponse.Resource.ResourceId);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);

            newUserId = Guid.NewGuid().ToString();
            userResponse.Resource.Id = newUserId;
            userResponse = await this.database.UpsertUserAsync(userResponse.Resource.Id);
            Assert.AreEqual(newUserId, userResponse.Resource.Id);
            SelflinkValidator.ValidateUserSelfLink(userResponse.Resource.SelfLink);
        }
    }
}
