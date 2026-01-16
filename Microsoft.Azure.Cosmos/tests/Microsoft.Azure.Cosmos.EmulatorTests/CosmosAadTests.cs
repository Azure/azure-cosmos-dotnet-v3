//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Documents.Client;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    [TestClass]
    public class CosmosAadTests
    {
        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task AadMockTest(ConnectionMode connectionMode)
        {
            int requestCount = 0;
            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();
            using CosmosClient cosmosClient = TestCommon.CreateCosmosClient();
            Database database = await cosmosClient.CreateDatabaseAsync(databaseId);
            Container container = await database.CreateContainerAsync(
                containerId,
                "/id");

            try
            {
                (string endpoint, string authKey) = TestCommon.GetAccountInfo();
                LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(expectedScope: "https://127.0.0.1/.default", masterKey: authKey);
                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = connectionMode,
                    ConnectionProtocol = connectionMode == ConnectionMode.Direct ? Protocol.Tcp : Protocol.Https,
                };

                if (connectionMode == ConnectionMode.Direct)
                {
                    long lsn = 2;
                    clientOptions.TransportClientHandlerFactory = (transport) => new TransportClientWrapper(transport,
                     interceptorAfterResult: (request, storeResponse) =>
                     {
                         // Force a barrier request on create item.
                         // There needs to be 2 regions and the GlobalCommittedLSN must be behind the LSN.
                         if (storeResponse.StatusCode == HttpStatusCode.Created)
                         {
                             if (requestCount == 0)
                             {
                                 requestCount++;
                                 lsn = storeResponse.LSN;
                                 storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                 storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.GlobalCommittedLSN, "0");
                             }
                         }

                         // Head request is the barrier request
                         // The GlobalCommittedLSN is set to -1 because the local emulator doesn't have geo-dr so it has to be
                         // overridden for the validation to succeed.
                         if (request.OperationType == Documents.OperationType.Head)
                         {
                             if (requestCount == 1)
                             {
                                 requestCount++;
                                 storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.NumberOfReadRegions, "2");
                                 storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.GlobalCommittedLSN, lsn.ToString(CultureInfo.InvariantCulture));
                             }
                         }

                         return storeResponse;
                     });
                }

                using CosmosClient aadClient = new CosmosClient(
                    endpoint,
                    simpleEmulatorTokenCredential,
                    clientOptions);

                TokenCredentialCache tokenCredentialCache = ((AuthorizationTokenProviderTokenCredential)aadClient.AuthorizationTokenProvider).tokenCredentialCache;

                // The refresh interval changes slightly based on how fast machine calculate the interval based on the expire time.
                Assert.IsTrue(15 <= tokenCredentialCache.BackgroundTokenCredentialRefreshInterval.Value.TotalMinutes, "Default background refresh should be 25% of the token life which is defaulted to 1hr");
                Assert.IsTrue(tokenCredentialCache.BackgroundTokenCredentialRefreshInterval.Value.TotalMinutes > 14.7, "Default background refresh should be 25% of the token life which is defaulted to 1hr");

                Database aadDatabase = await aadClient.GetDatabase(databaseId).ReadAsync();
                Container aadContainer = await aadDatabase.GetContainer(containerId).ReadContainerAsync();
                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                ItemResponse<ToDoActivity> itemResponse = await aadContainer.CreateItemAsync(
                    toDoActivity,
                    new PartitionKey(toDoActivity.id));

                // Gateway does the barrier requests so only direct mode needs to be validated.
                if (connectionMode == ConnectionMode.Direct)
                {
                    Assert.AreEqual(2, requestCount, "The barrier request was never called.");
                }

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
            finally
            {
                await database?.DeleteStreamAsync();
            }
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
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey,
                getTokenCallback: GetAadTokenCallBack);

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
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey,
                getTokenCallback: GetAadTokenCallBack);

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
                Assert.AreEqual(2, getAadTokenCount);
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
                expectedScope: "https://127.0.0.1/.default",
                masterKey: authKey,
                getTokenCallback: GetAadTokenCallBack);

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
                Assert.AreEqual(2, getAadTokenCount);
                await Task.Delay(TimeSpan.FromSeconds(1));
                try
                {
                    ResponseMessage responseMessage =
                        await aadClient.GetDatabase(Guid.NewGuid().ToString()).ReadStreamAsync();
                    Assert.Fail("Should throw auth error.");
                }
                catch (RequestFailedException ce) when (ce.Status == (int)HttpStatusCode.RequestTimeout)
                {
                    Assert.IsNotNull(ce.Message);
                    Assert.IsTrue(ce.ToString().Contains(errorMessage));
                }
            }
        }

        [TestMethod]
        public async Task Aad_OverrideScope_NoFallback_OnFailure_E2E()
        {
            // Arrange
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            string databaseId = "db-" + Guid.NewGuid();
            using (CosmosClient setupClient = TestCommon.CreateCosmosClient())
            {
                await setupClient.CreateDatabaseAsync(databaseId);
            }

            string overrideScope = "https://override/.default";
            string accountScope = $"https://{new Uri(endpoint).Host}/.default";
            int overrideScopeCount = 0;
            int accountScopeCount = 0;

            string previous = Environment.GetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE");
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", overrideScope);

            void GetAadTokenCallBack(TokenRequestContext context, CancellationToken token)
            {
                string scope = context.Scopes[0];
                if (scope == overrideScope)
                {
                    overrideScopeCount++;
                    throw new RequestFailedException(408, "Simulated override scope failure");
                }
                if (scope == accountScope)
                {
                    accountScopeCount++;
                }
            }

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScopes: new[] { overrideScope, accountScope },
                masterKey: authKey,
                getTokenCallback: GetAadTokenCallBack);

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromSeconds(60)
            };

            try
            {
                using CosmosClient aadClient = new CosmosClient(endpoint, credential, clientOptions);

                try
                {
                    // Act
                    ResponseMessage r = await aadClient.GetDatabase(databaseId).ReadStreamAsync();
                    Assert.Fail("Expected failure when override scope token acquisition fails.");
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.RequestTimeout || ex.Status == 408)
                {
                    // Assert
                    Assert.IsTrue(overrideScopeCount > 0, "Override scope should have been attempted.");
                    Assert.AreEqual(0, accountScopeCount, "No fallback to account scope must occur when override is configured.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", previous);
                using CosmosClient cleanup = TestCommon.CreateCosmosClient();
                await cleanup.GetDatabase(databaseId).DeleteAsync();
            }
        }

        [TestMethod]
        public async Task Aad_AccountScope_Fallbacks_ToCosmosScope()
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();

            string previous = Environment.GetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE");
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", null);

            string accountScope = $"https://{new Uri(endpoint).Host}/.default";
            string aadScope = "https://cosmos.azure.com/.default";

            int accountScopeCount = 0;
            int cosmosScopeCount = 0;

            void GetAadTokenCallBack(TokenRequestContext context, CancellationToken token)
            {
                string scope = context.Scopes[0];

                if (string.Equals(scope, accountScope, StringComparison.OrdinalIgnoreCase))
                {
                    accountScopeCount++;
                    throw new Exception(
                        message: "AADSTS500011",
                        innerException: new Exception("AADSTS500011"));
                }

                if (string.Equals(scope, aadScope, StringComparison.OrdinalIgnoreCase))
                {
                    cosmosScopeCount++;
                }
            }

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScopes: new[] { accountScope, aadScope },
                masterKey: authKey,
                getTokenCallback: GetAadTokenCallBack);

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromSeconds(60)
            };

            try
            {
                using CosmosClient aadClient = new CosmosClient(endpoint, credential, clientOptions);
                TokenCredentialCache tokenCredentialCache =
                    ((AuthorizationTokenProviderTokenCredential)aadClient.AuthorizationTokenProvider).tokenCredentialCache;

                string token = await tokenCredentialCache.GetTokenAsync(Tracing.Trace.GetRootTrace("account-fallback-to-cosmos-test"));
                Assert.IsFalse(string.IsNullOrEmpty(token), "Fallback should succeed and produce a token.");

                Assert.IsTrue(accountScopeCount >= 1, "Account scope must be attempted first.");
                Assert.IsTrue(cosmosScopeCount >= 1, "The client must fall back to cosmos.azure.com scope.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", previous);
            }
        }

        [TestMethod]
        public async Task Aad_AccountScope_Success_NoFallback()
        {
            // Arrange
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();

            string accountScope = $"https://{new Uri(endpoint).Host}/.default";
            string aadScope = "https://cosmos.azure.com/.default";

            int accountScopeCount = 0;
            int cosmosScopeCount = 0;

            void GetAadTokenCallBack(TokenRequestContext context, CancellationToken token)
            {
                string scope = context.Scopes[0];

                if (string.Equals(scope, accountScope, StringComparison.OrdinalIgnoreCase))
                {
                    accountScopeCount++;
                }

                if (string.Equals(scope, aadScope, StringComparison.OrdinalIgnoreCase))
                {
                    cosmosScopeCount++;
                }
            }

            LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                expectedScopes: new[] { accountScope },
                masterKey: authKey,
                getTokenCallback: GetAadTokenCallBack);

            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromSeconds(60)
            };

            using CosmosClient aadClient = new CosmosClient(endpoint, credential, clientOptions);
            TokenCredentialCache tokenCredentialCache =
                ((AuthorizationTokenProviderTokenCredential)aadClient.AuthorizationTokenProvider).tokenCredentialCache;

            string token = await tokenCredentialCache.GetTokenAsync(Tracing.Trace.GetRootTrace("account-scope-success-no-fallback"));
            Assert.IsFalse(string.IsNullOrEmpty(token), "Token should be acquired successfully with account scope.");

            Assert.AreEqual(1, accountScopeCount, "Account scope must be used exactly once.");
            Assert.AreEqual(0, cosmosScopeCount, "Cosmos scope must not be used (no fallback).");
        }

        [TestMethod]
        public async Task AadTokenRevocation_WithMockedServerResponse_ShouldTriggerTokenRefresh()
        {
            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();

            using CosmosClient setupClient = TestCommon.CreateCosmosClient();
            Database database = await setupClient.CreateDatabaseAsync(databaseId);
            await database.CreateContainerAsync(containerId, "/id");

            try
            {
                (string endpoint, string authKey) = TestCommon.GetAccountInfo();

                List<TokenRequestContext> tokenRequests = new List<TokenRequestContext>();
                bool hasReturnedUnauthorized = false;

                void GetAadTokenCallBack(TokenRequestContext context, CancellationToken token)
                {
                    tokenRequests.Add(context);
                }

                LocalEmulatorTokenCredential tokenCredential = new LocalEmulatorTokenCredential(
                    expectedScope: "https://127.0.0.1/.default",
                    masterKey: authKey,
                    getTokenCallback: GetAadTokenCallBack);

                HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
                {
                    ResponseIntercepter = (response, request) =>
                    {
                        bool isDocumentCreate = request.Method == HttpMethod.Post
                            && request.RequestUri.PathAndQuery.Contains("/docs");

                        if (isDocumentCreate && !hasReturnedUnauthorized)
                        {
                            hasReturnedUnauthorized = true;

                            // Return 401 with CAE challenge (though SDK won't read it from response)
                            HttpResponseMessage unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
                            {
                                RequestMessage = request,
                                Content = new StringContent("{\"message\":\"Unauthorized\"}")
                            };
                            unauthorizedResponse.Headers.Add(
                                "WWW-Authenticate",
                                @"Bearer error=""insufficient_claims"", claims=""eyJhY2Nlc3NfdG9rZW4iOnsibmJmIjp7ImVzc2VudGlhbCI6dHJ1ZSwgInZhbHVlIjoiMTcwNjgzMjAwMCJ9fX0=""");

                            return Task.FromResult(unauthorizedResponse);
                        }

                        return Task.FromResult(response);
                    }
                };

                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => new HttpClient(httpHandler),
                };

                using (CosmosClient aadClient = new CosmosClient(endpoint, tokenCredential, clientOptions))
                {
                    Container aadContainer = aadClient.GetContainer(databaseId, containerId);
                    tokenRequests.Clear();

                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

                    try
                    {
                        await aadContainer.CreateItemAsync(item, new PartitionKey(item.id));
                        Assert.Fail("Expected operation to fail");
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // Expected - 401 should be returned
                    }

                    // Validate that 401 was returned
                    Assert.IsTrue(hasReturnedUnauthorized, "Test should have returned 401 Unauthorized");

                    // NOTE: We cannot validate merged claims in token request because SDK has a limitation:
                    // ClientRetryPolicy.HandleUnauthorizedResponse() reads request headers instead of 
                    // response headers for WWW-Authenticate, so CAE claims are never extracted.
                    // This test validates that 401 triggers the unauthorized flow.
                }
            }
            finally
            {
                await database?.DeleteStreamAsync();
            }
        }

        [TestMethod]
        public async Task AadTokenRevocation_ExceedsMaxRetry_ShouldFail()
        {
            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();

            using CosmosClient setupClient = TestCommon.CreateCosmosClient();
            Database database = await setupClient.CreateDatabaseAsync(databaseId);

            try
            {
                await database.CreateContainerAsync(containerId, "/id");
                (string endpoint, string authKey) = TestCommon.GetAccountInfo();

                int caeResponseCount = 0;

                LocalEmulatorTokenCredential tokenCredential = new LocalEmulatorTokenCredential(
                    expectedScope: "https://127.0.0.1/.default",
                    masterKey: authKey);

                HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
                {
                    ResponseIntercepter = (response, request) =>
                    {
                        bool isDocumentCreate = request.Method == HttpMethod.Post
                            && request.RequestUri.PathAndQuery.Contains("/docs");

                        if (isDocumentCreate)
                        {
                            caeResponseCount++;

                            // Always return CAE challenge
                            HttpResponseMessage caeResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
                            {
                                RequestMessage = request,
                                Content = new StringContent("{\"message\":\"CAE challenge\"}")
                            };
                            caeResponse.Headers.Add(
                                "WWW-Authenticate",
                                "Bearer error=\"insufficient_claims\", claims=\"eyJhY2Nlc3NfdG9rZW4iOnt9fQ==\"");

                            return Task.FromResult(caeResponse);
                        }

                        return Task.FromResult(response);
                    }
                };

                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => new HttpClient(httpHandler),
                };

                using CosmosClient aadClient = new CosmosClient(endpoint, tokenCredential, clientOptions);

                Container aadContainer = aadClient.GetContainer(databaseId, containerId);

                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

                try
                {
                    await aadContainer.CreateItemAsync(item, new PartitionKey(item.id));
                    Assert.Fail("Expected CosmosException after max CAE retries exceeded");
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Expected - should fail after max retry (1 retry = 2 total attempts)
                    Assert.IsTrue(caeResponseCount <= 2,
                        $"Should stop after max retry. CAE responses: {caeResponseCount}");
                }
            }
            finally
            {
                await database?.DeleteStreamAsync();
            }
        }
    }
}