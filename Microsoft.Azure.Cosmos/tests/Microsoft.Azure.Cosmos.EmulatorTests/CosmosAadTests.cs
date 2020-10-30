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

            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.AreEqual(1, getAadTokenCount);
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

        private const string AccountEndpointHost = "test-account.documents.azure.com";
        private const string ExpectedScope = "https://test-account.documents.azure.com/.default";

        private readonly AccessToken AccessToken = new AccessToken("AccessToken", DateTimeOffset.MaxValue);

        [TestMethod]
        public async Task TestTokenCredentialCacheHappyPathAsync()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(() => new ValueTask<AccessToken>(this.AccessToken));

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                await this.GetAndVerifyTokenAsync(tokenCredentialCache);
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialTimeoutAsync()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(async () => 
            {
                await Task.Delay(-1);

                return new AccessToken("AccessToken", DateTimeOffset.MaxValue);
            });

            TimeSpan timeout = TimeSpan.FromSeconds(1);
            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(
                tokenCredential: testTokenCredential,
                requestTimeout: timeout))
            {
                try
                {
                    await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore());
                    Assert.Fail("TokenCredentialCache.GetTokenAsync() is expected to fail but succeeded");
                }
                catch (CosmosException cosmosException)
                {
                    Assert.AreEqual(HttpStatusCode.Unauthorized, cosmosException.StatusCode);
                    Assert.AreEqual((int)Azure.Documents.SubStatusCodes.FailedToGetAadToken, cosmosException.SubStatusCode);
                    Assert.AreEqual($"TokenCredential.GetTokenAsync request timed out after {timeout}", cosmosException.InnerException.Message);
                }
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialErrorAsync()
        {
            Exception exception = new Exception();

            TestTokenCredential testTokenCredential = new TestTokenCredential(() => throw exception);

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                try
                {
                    await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore());
                    Assert.Fail("TokenCredentialCache.GetTokenAsync() is expected to fail but succeeded");
                }
                catch (CosmosException cosmosException)
                {
                    Assert.AreEqual(HttpStatusCode.Unauthorized, cosmosException.StatusCode);
                    Assert.AreEqual((int)Azure.Documents.SubStatusCodes.FailedToGetAadToken, cosmosException.SubStatusCode);

                    Assert.IsTrue(object.ReferenceEquals(
                        exception,
                        cosmosException.InnerException));
                }

                // TokenCredential.GetTokenAsync() is retried for 3 times, so it should have been invoked for 4 times.
                Assert.AreEqual(3, testTokenCredential.NumTimesInvoked);
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialBackgroundRefreshAsync()
        {
            // When token is within tokenCredentialRefreshBuffer of expiry, start background task to refresh token,
            // but return the cached token.
            string token1 = "Token1";
            string token2 = "Token2";
            bool firstTimeGetToken = true;

            TestTokenCredential testTokenCredential = new TestTokenCredential(() =>
            {
                if (firstTimeGetToken)
                {
                    firstTimeGetToken = false;

                    return new ValueTask<AccessToken>(new AccessToken(token1, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10)));
                }
                else
                {
                    Task.Delay(TimeSpan.FromSeconds(.5)).Wait();

                    return new ValueTask<AccessToken>(new AccessToken(token2, DateTimeOffset.MaxValue));
                }
            });

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                string t1 = await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore());
                Assert.AreEqual(token1, t1);

                // Token is valid for 6 seconds. Client TokenCredentialRefreshBuffer is set to 5 seconds.
                // After waiting for 2 seconds, the cache token is still valid, but it will be refreshed in the background.
                await Task.Delay(TimeSpan.FromSeconds(2));
                string t2 = await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore());
                Assert.AreEqual(token1, t2);

                // After waiting for another 3 seconds (5 seconds for background refresh), token1 is still valid,
                // but cached token has been refreshed to token2 by the background task started before.
                await Task.Delay(TimeSpan.FromSeconds(3.6));
                string t3 = await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore());
                Assert.AreEqual(token2, t3);

                Assert.AreEqual(2, testTokenCredential.NumTimesInvoked);
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialFailedToRefreshAsync()
        {
            string token = "Token";
            bool firstTimeGetToken = true;
            Exception exception = new Exception();

            TestTokenCredential testTokenCredential = new TestTokenCredential(() =>
            {
                if (firstTimeGetToken)
                {
                    firstTimeGetToken = false;

                    return new ValueTask<AccessToken>(new AccessToken(token, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(6)));
                }
                else
                {
                    throw exception;
                }
            });

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                Assert.AreEqual(token, await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore()));

                // Token is valid for 6 seconds. Client TokenCredentialRefreshBuffer is set to 5 seconds.
                // After waiting for 2 seconds, the cache token is still valid, but it will be refreshed in the background.
                await Task.Delay(TimeSpan.FromSeconds(2));
                Assert.AreEqual(token, await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore()));

                // Token refreshes fails except for the first time, but the cached token will be served as long as it is valid.
                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.AreEqual(token, await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore()));

                // Cache token has expired, and it fails to refresh.
                await Task.Delay(TimeSpan.FromSeconds(2));

                try
                {
                    await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore());
                    Assert.Fail("TokenCredentialCache.GetTokenAsync() is expected to fail but succeeded");
                }
                catch (CosmosException cosmosException)
                {
                    Assert.AreEqual(HttpStatusCode.Unauthorized, cosmosException.StatusCode);
                    Assert.AreEqual((int)Azure.Documents.SubStatusCodes.FailedToGetAadToken, cosmosException.SubStatusCode);

                    Assert.IsTrue(object.ReferenceEquals(
                        exception,
                        cosmosException.InnerException));
                }
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialMultiThreadAsync()
        {
            // When multiple thread calls TokenCredentialCache.GetTokenAsync and a valid cached token
            // is not available, TokenCredentialCache will only create one task to get token.
            int numTasks = 100;

            TestTokenCredential testTokenCredential = new TestTokenCredential(() =>
            {
                Task.Delay(TimeSpan.FromSeconds(3)).Wait();

                return new ValueTask<AccessToken>(this.AccessToken);
            });

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                Task[] tasks = new Task[numTasks];

                for (int i = 0; i < numTasks; i++)
                {
                    tasks[i] = this.GetAndVerifyTokenAsync(tokenCredentialCache);
                }

                await Task.WhenAll(tasks);

                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);
            }
        }

        private TokenCredentialCache CreateTokenCredentialCache(
            TokenCredential tokenCredential, 
            TimeSpan? requestTimeout = null)
        {
            return new TokenCredentialCache(
                tokenCredential,
                CosmosAadTests.AccountEndpointHost,
                requestTimeout: requestTimeout ?? TimeSpan.FromSeconds(15),
                backgroundTokenCredentialRefreshInterval: TimeSpan.FromSeconds(5));
        }

        private async Task GetAndVerifyTokenAsync(TokenCredentialCache tokenCredentialCache)
        {
            Assert.AreEqual(
                this.AccessToken.Token,
                await tokenCredentialCache.GetTokenAsync(new CosmosDiagnosticsContextCore()));
        }

        private sealed class TestTokenCredential : TokenCredential
        {
            public int NumTimesInvoked { get; private set; } = 0;

            private readonly Func<ValueTask<AccessToken>> accessTokenFunc;

            public TestTokenCredential(Func<ValueTask<AccessToken>> accessTokenFunc)
            {
                this.accessTokenFunc = accessTokenFunc;
            }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.NumTimesInvoked++;

                Assert.AreEqual(1, requestContext.Scopes.Length);
                Assert.AreEqual(CosmosAadTests.ExpectedScope, requestContext.Scopes[0]);

                return this.accessTokenFunc().Result;
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.NumTimesInvoked++;

                Assert.AreEqual(1, requestContext.Scopes.Length);
                Assert.AreEqual(CosmosAadTests.ExpectedScope, requestContext.Scopes[0]);

                return this.accessTokenFunc();
            }
        }
    }
}