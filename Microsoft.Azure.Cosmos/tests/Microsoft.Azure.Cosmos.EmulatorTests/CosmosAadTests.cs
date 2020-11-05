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
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task AadMockTest(ConnectionMode connectionMode)
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
                ConnectionMode = connectionMode,
                ConnectionProtocol = connectionMode == ConnectionMode.Direct ? Protocol.Tcp : Protocol.Https
            };

            using CosmosClient aadClient = new CosmosClient(
                endpoint,
                simpleEmulatorTokenCredential,
                clientOptions);

            TokenCredentialCache tokenCredentialCache = ((AuthorizationTokenProviderTokenCredential)aadClient.AuthorizationTokenProvider).tokenCredentialCache;

            // The refresh interval changes slightly based on how fast machine calculate the interval based on the expire time.
            Assert.IsTrue(15 <= tokenCredentialCache.BackgroundTokenCredentialRefreshInterval.Value.TotalMinutes, "Default background refresh should be 25% of the token life which is defaulted to 1hr");
            Assert.IsTrue(tokenCredentialCache.BackgroundTokenCredentialRefreshInterval.Value.TotalMinutes > 14.7 , "Default background refresh should be 25% of the token life which is defaulted to 1hr");

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
            void GetAadTokenCallBack(
                TokenRequestContext context,
                CancellationToken token)
            {
                getAadTokenCount++;
            }

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

            DocumentClient documentClient = aadClient.ClientContext.DocumentClient;
            TokenCredentialCache tokenCredentialCache = ((AuthorizationTokenProviderTokenCredential)aadClient.AuthorizationTokenProvider).tokenCredentialCache;

            Assert.AreEqual(TimeSpan.FromSeconds(1), tokenCredentialCache.BackgroundTokenCredentialRefreshInterval);
            Assert.AreEqual(1, getAadTokenCount);

            await aadClient.ReadAccountAsync();
            await aadClient.ReadAccountAsync();
            await aadClient.ReadAccountAsync();

            // Should use cached token
            Assert.AreEqual(1, getAadTokenCount);

            // Token should be refreshed after 1 second
            await Task.Delay(TimeSpan.FromSeconds(1.2));
            Assert.AreEqual(2, getAadTokenCount);
        }

        [TestMethod]
        public async Task AadMockRefreshRetryTest()
        {
            int getAadTokenCount = 0;
            void GetAadTokenCallBack(
                TokenRequestContext context,
                CancellationToken token)
            {
                getAadTokenCount++;
                if (getAadTokenCount <= 2)
                {
                    throw new RequestFailedException(
                        408,
                        "Test Failure");
                }
            }

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
            void GetAadTokenCallBack(
                TokenRequestContext context,
                CancellationToken token)
            {
                getAadTokenCount++;
                throw new RequestFailedException(
                    408,
                    errorMessage);
            }

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