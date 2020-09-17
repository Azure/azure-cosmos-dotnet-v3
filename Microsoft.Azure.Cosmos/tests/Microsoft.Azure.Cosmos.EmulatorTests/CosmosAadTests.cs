//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Documents.Client;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.IdentityModel.Tokens;

    [TestClass]
    public class CosmosAadTests
    {
        [TestMethod]
        public async Task AadMockTest()
        {
            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();
            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient())
            {
                Database database = await cosmosClient.CreateDatabaseAsync(databaseId);
                Container container = await database.CreateContainerAsync(
                    containerId,
                    "/id");
            }

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(authKey);
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConnectionProtocol = Protocol.Https
            };

            using CosmosClient aadClient = new CosmosClient(
                endpoint,
                simpleEmulatorTokenCredential,
                clientOptions);

            Database aadDatabase = await aadClient.GetDatabase(databaseId).ReadAsync();
            Container aadContainer = await aadDatabase.GetContainer(containerId).ReadContainerAsync();
            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> itemResponse = await aadContainer.CreateItemAsync(
                toDoActivity,
                new PartitionKey(toDoActivity.id));

            toDoActivity.cost = 42.42;
            await aadContainer.ReplaceItemAsync(
                toDoActivity,
                toDoActivity.id,
                new PartitionKey(toDoActivity.id));

            await aadContainer.ReadItemAsync<ToDoActivity>(
                toDoActivity.id,
                new PartitionKey(toDoActivity.id));

            await aadContainer.UpsertItemAsync(toDoActivity);

            await aadContainer.DeleteItemAsync<ToDoActivity>(
                toDoActivity.id,
                new PartitionKey(toDoActivity.id));
        }

        [TestMethod]
        public async Task AadMockRefreshTest()
        {
            int getAadTokenCount = 0;
            Action<TokenRequestContext, CancellationToken> GetAadTokenCallBack = (
                context,
                token) =>
            {
                getAadTokenCount++;
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(
                authKey,
                GetAadTokenCallBack);

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromSeconds(1)
            };

            Assert.AreEqual(0, getAadTokenCount);
            using CosmosClient aadClient = new CosmosClient(
                endpoint,
                simpleEmulatorTokenCredential,
                clientOptions);

            Assert.AreEqual(1, getAadTokenCount);

            await aadClient.ReadAccountAsync();
            await aadClient.ReadAccountAsync();
            await aadClient.ReadAccountAsync();

            // Should use cached token
            Assert.AreEqual(1, getAadTokenCount);

            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.AreEqual(1, getAadTokenCount);
        }

        [TestMethod]
        public async Task AadMockRefreshRetryTest()
        {
            int getAadTokenCount = 0;
            Action<TokenRequestContext, CancellationToken> GetAadTokenCallBack = (
                context,
                token) =>
            {
                getAadTokenCount++;
                if (getAadTokenCount <= 2)
                {
                    throw new RequestFailedException(
                        408,
                        "Test Failure");
                }
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(
                authKey,
                GetAadTokenCallBack);

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromSeconds(60)
            };

            Assert.AreEqual(0, getAadTokenCount);
            using (CosmosClient aadClient = new CosmosClient(
                endpoint,
                simpleEmulatorTokenCredential,
                clientOptions))
            {
                Assert.AreEqual(3, getAadTokenCount);
                await Task.Delay(TimeSpan.FromSeconds(1));
                ResponseMessage responseMessage = await aadClient.GetDatabase(Guid.NewGuid().ToString()).ReadStreamAsync();
                Assert.IsNotNull(responseMessage);

                // Should use cached token
                Assert.AreEqual(3, getAadTokenCount);
            }
        }

        [TestMethod]
        public async Task AadMockNegativeRefreshRetryTest()
        {
            int getAadTokenCount = 0;
            string errorMessage = "Test Failure" + Guid.NewGuid();
            Action<TokenRequestContext, CancellationToken> GetAadTokenCallBack = (
                context,
                token) =>
            {
                getAadTokenCount++;
                throw new RequestFailedException(
                    408,
                    errorMessage);
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(
                authKey,
                GetAadTokenCallBack);

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromSeconds(60)
            };

            Assert.AreEqual(0, getAadTokenCount);
            using (CosmosClient aadClient = new CosmosClient(
                endpoint,
                simpleEmulatorTokenCredential,
                clientOptions))
            {
                Assert.AreEqual(3, getAadTokenCount);
                await Task.Delay(TimeSpan.FromSeconds(1));
                try
                {
                    ResponseMessage responseMessage =
                        await aadClient.GetDatabase(Guid.NewGuid().ToString()).ReadStreamAsync();
                    Assert.Fail("Should throw auth error.");
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Assert.IsNotNull(ce.Message);
                    Assert.IsTrue(ce.ToString().Contains(errorMessage));
                }
            }
        }
    }
}