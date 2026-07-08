//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using ResourceType = Documents.ResourceType;

    [TestClass]
    public class CosmosAuthorizationTests
    {
        private static readonly Uri AccountEndpoint = new Uri("https://test-account.documents.azure.com");
        private const string ExpectedScope = "https://test-account.documents.azure.com/.default";

        private readonly AccessToken AccessToken = new AccessToken("AccessToken", DateTimeOffset.MaxValue);


        public CosmosAuthorizationTests()
        {
        }

        [TestMethod]
        public async Task ResourceTokenAsync()
        {
            using AuthorizationTokenProvider cosmosAuthorization = new AuthorizationTokenProviderResourceToken("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=");

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test",
                    ResourceType.Database.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc%3d", token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc",
                    ResourceType.Collection.ToResourceTypeString(),
                    "PUT",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc%3d", token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc\\docs\\1234",
                    ResourceType.Document.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual("VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc%3d", token);
                Assert.IsNull(payload);
            }
        }

        [TestMethod]
        public async Task TokenAuthAsync()
        {
            LocalEmulatorTokenCredential simpleEmulatorTokenCredential = new LocalEmulatorTokenCredential(
                expectedScope: "https://127.0.0.1/.default",
                masterKey: "VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=",
                defaultDateTime: new DateTime(2030, 9, 21, 9, 9, 9, DateTimeKind.Utc));

            using AuthorizationTokenProvider cosmosAuthorization = new AuthorizationTokenProviderTokenCredential(
                simpleEmulatorTokenCredential,
                new Uri("https://127.0.0.1:8081"),
                backgroundTokenCredentialRefreshInterval: TimeSpan.FromSeconds(1),
                AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test",
                    ResourceType.Database.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dewogICAgICAgICAgICAgICAgImFsZyI6IlJTMjU2IiwKICAgICAgICAgICAgICAgICJraWQiOiJ4XzlLU3VzS1U1WWNIZjQiLAogICAgICAgICAgICAgICAgInR5cCI6IkpXVCIKICAgICAgICAgICAgfQ.ewogICAgICAgICAgICAgICAgIm9pZCI6Ijk2MzEzMDM0LTQ3MzktNDNjYi05M2NkLTc0MTkzYWRiZTViNiIsCiAgICAgICAgICAgICAgICAidGlkIjoiN2IxOTk5YTEtZGZkNy00NDBlLTgyMDQtMDAxNzA5NzliOTg0IiwKICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLAogICAgICAgICAgICAgICAgImdyb3VwcyI6WwogICAgICAgICAgICAgICAgICAgICI3Y2UxZDAwMy00Y2IzLTQ4NzktYjdjNS03NDA2MmEzNWM2NmUiLAogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLAogICAgICAgICAgICAgICAgICAgICI1NTQ5YmI2Mi1jNzdiLTQzMDUtYmRhOS05ZWM2NmI4NWQ5ZTQiLAogICAgICAgICAgICAgICAgICAgICJjNDRmZDY4NS01YzU4LTQ1MmMtYWFmNy0xM2NlNzUxODRmNjUiLAogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiCiAgICAgICAgICAgICAgICBdLAogICAgICAgICAgICAgICAgIm5iZiI6MTkxNjIxMjE0OSwKICAgICAgICAgICAgICAgICJleHAiOjE5MTYyMTU3NDksCiAgICAgICAgICAgICAgICAiaWF0IjoxNTk2NTkyMzM1LAogICAgICAgICAgICAgICAgImlzcyI6Imh0dHBzOi8vc3RzLmZha2UtaXNzdWVyLm5ldC83YjE5OTlhMS1kZmQ3LTQ0MGUtODIwNC0wMDE3MDk3OWI5ODQiLAogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCIKICAgICAgICAgICAgfQ.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0",
                    token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc",
                    ResourceType.Collection.ToResourceTypeString(),
                    "PUT",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dewogICAgICAgICAgICAgICAgImFsZyI6IlJTMjU2IiwKICAgICAgICAgICAgICAgICJraWQiOiJ4XzlLU3VzS1U1WWNIZjQiLAogICAgICAgICAgICAgICAgInR5cCI6IkpXVCIKICAgICAgICAgICAgfQ.ewogICAgICAgICAgICAgICAgIm9pZCI6Ijk2MzEzMDM0LTQ3MzktNDNjYi05M2NkLTc0MTkzYWRiZTViNiIsCiAgICAgICAgICAgICAgICAidGlkIjoiN2IxOTk5YTEtZGZkNy00NDBlLTgyMDQtMDAxNzA5NzliOTg0IiwKICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLAogICAgICAgICAgICAgICAgImdyb3VwcyI6WwogICAgICAgICAgICAgICAgICAgICI3Y2UxZDAwMy00Y2IzLTQ4NzktYjdjNS03NDA2MmEzNWM2NmUiLAogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLAogICAgICAgICAgICAgICAgICAgICI1NTQ5YmI2Mi1jNzdiLTQzMDUtYmRhOS05ZWM2NmI4NWQ5ZTQiLAogICAgICAgICAgICAgICAgICAgICJjNDRmZDY4NS01YzU4LTQ1MmMtYWFmNy0xM2NlNzUxODRmNjUiLAogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiCiAgICAgICAgICAgICAgICBdLAogICAgICAgICAgICAgICAgIm5iZiI6MTkxNjIxMjE0OSwKICAgICAgICAgICAgICAgICJleHAiOjE5MTYyMTU3NDksCiAgICAgICAgICAgICAgICAiaWF0IjoxNTk2NTkyMzM1LAogICAgICAgICAgICAgICAgImlzcyI6Imh0dHBzOi8vc3RzLmZha2UtaXNzdWVyLm5ldC83YjE5OTlhMS1kZmQ3LTQ0MGUtODIwNC0wMDE3MDk3OWI5ODQiLAogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCIKICAgICAgICAgICAgfQ.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0", token);
                Assert.IsNull(payload);
            }

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test\\colls\\abc\\docs\\1234",
                    ResourceType.Document.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dewogICAgICAgICAgICAgICAgImFsZyI6IlJTMjU2IiwKICAgICAgICAgICAgICAgICJraWQiOiJ4XzlLU3VzS1U1WWNIZjQiLAogICAgICAgICAgICAgICAgInR5cCI6IkpXVCIKICAgICAgICAgICAgfQ.ewogICAgICAgICAgICAgICAgIm9pZCI6Ijk2MzEzMDM0LTQ3MzktNDNjYi05M2NkLTc0MTkzYWRiZTViNiIsCiAgICAgICAgICAgICAgICAidGlkIjoiN2IxOTk5YTEtZGZkNy00NDBlLTgyMDQtMDAxNzA5NzliOTg0IiwKICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLAogICAgICAgICAgICAgICAgImdyb3VwcyI6WwogICAgICAgICAgICAgICAgICAgICI3Y2UxZDAwMy00Y2IzLTQ4NzktYjdjNS03NDA2MmEzNWM2NmUiLAogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLAogICAgICAgICAgICAgICAgICAgICI1NTQ5YmI2Mi1jNzdiLTQzMDUtYmRhOS05ZWM2NmI4NWQ5ZTQiLAogICAgICAgICAgICAgICAgICAgICJjNDRmZDY4NS01YzU4LTQ1MmMtYWFmNy0xM2NlNzUxODRmNjUiLAogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiCiAgICAgICAgICAgICAgICBdLAogICAgICAgICAgICAgICAgIm5iZiI6MTkxNjIxMjE0OSwKICAgICAgICAgICAgICAgICJleHAiOjE5MTYyMTU3NDksCiAgICAgICAgICAgICAgICAiaWF0IjoxNTk2NTkyMzM1LAogICAgICAgICAgICAgICAgImlzcyI6Imh0dHBzOi8vc3RzLmZha2UtaXNzdWVyLm5ldC83YjE5OTlhMS1kZmQ3LTQ0MGUtODIwNC0wMDE3MDk3OWI5ODQiLAogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCIKICAgICAgICAgICAgfQ.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0", token);
                Assert.IsNull(payload);
            }
        }

        [TestMethod]
        public async Task AadAuthorizationSignatureCaching_ReturnsCachedResultForSameToken()
        {
            Mock<TokenCredential> mockCredential = new Mock<TokenCredential>();
            mockCredential.Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccessToken("test-aad-token-value", DateTimeOffset.UtcNow.AddHours(1)));

            using TokenCredentialCache cache = new TokenCredentialCache(
                mockCredential.Object,
                CosmosAuthorizationTests.AccountEndpoint,
                backgroundTokenCredentialRefreshInterval: TimeSpan.MaxValue,
                tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");
            string result1 = await cache.GetTokenAuthorizationHeaderAsync(trace);
            string result2 = await cache.GetTokenAuthorizationHeaderAsync(trace);

            // Same reference means the cached value was returned (not recomputed)
            Assert.AreSame(result1, result2, "Expected cached authorization header to be returned for the same token.");

            // Verify the result matches the static method
            string expected = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("test-aad-token-value");
            Assert.AreEqual(expected, result1);

            // Token credential should only be called once (cached after that)
            mockCredential.Verify(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AadAuthorizationSignatureCaching_RecomputesForNewToken()
        {
            int callCount = 0;
            Mock<TokenCredential> mockCredential = new Mock<TokenCredential>();
            mockCredential.Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return callCount == 1
                        ? new AccessToken("first-aad-token", DateTimeOffset.UtcNow.AddMilliseconds(1))
                        : new AccessToken("second-aad-token", DateTimeOffset.UtcNow.AddHours(1));
                });

            using TokenCredentialCache cache = new TokenCredentialCache(
                mockCredential.Object,
                CosmosAuthorizationTests.AccountEndpoint,
                backgroundTokenCredentialRefreshInterval: TimeSpan.MaxValue,
                tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");
            string result1 = await cache.GetTokenAuthorizationHeaderAsync(trace);

            // Wait for first token to expire
            await Task.Delay(50);

            string result2 = await cache.GetTokenAuthorizationHeaderAsync(trace);

            Assert.AreNotEqual(result1, result2, "Different tokens should produce different authorization headers.");

            string expected1 = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("first-aad-token");
            string expected2 = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("second-aad-token");
            Assert.AreEqual(expected1, result1);
            Assert.AreEqual(expected2, result2);
        }

        [DataTestMethod]
        [DataRow("https://env-override/.default", "https://env-override/.default", DisplayName = "EnvVarOverride")]
        [DataRow("https://cosmos.azure.com/.default", "https://cosmos.azure.com/.default", DisplayName = "EnvVarOverride_Fabric")]
        [DataRow(null, "https://anyhost.documents.azure.com/.default", DisplayName = "NoEnvVar_DefaultScope")]
        public async Task TokenCredentialCache_SetsCorrectScope_EnvOverrideOrDefault(string envVarValue, string expectedScope)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", envVarValue);

            try
            {
                string anyHost = "anyhost.documents.azure.com";
                Uri anyUri = new Uri($"https://{anyHost}");

                LocalEmulatorTokenCredential credential = new LocalEmulatorTokenCredential(
                    masterKey: "testkey",
                    expectedScope: expectedScope);

                using (AuthorizationTokenProvider authorization = new AuthorizationTokenProviderTokenCredential(
                    credential,
                    anyUri,
                    backgroundTokenCredentialRefreshInterval: TimeSpan.FromSeconds(1),
                    AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature))
                {
                    StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                    (string token, string payload) = await authorization.GetUserAuthorizationAsync(
                        "dbs\\test",
                        ResourceType.Database.ToResourceTypeString(),
                        "GET",
                        headers,
                        AuthorizationTokenType.PrimaryMasterKey);

                    Assert.IsFalse(string.IsNullOrEmpty(token));
                    Assert.IsNull(payload);
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"Test failed with exception: {ex}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", null);
            }
        }

        [TestMethod]
        public void TestTokenCredentialCacheMaxAndMinValues()
        {
            try
            {
                TimeSpan toLarge = TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1);
                new TokenCredentialCache(
                    new Mock<TokenCredential>().Object,
                    CosmosAuthorizationTests.AccountEndpoint,
                    backgroundTokenCredentialRefreshInterval: toLarge,
                    tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);
                Assert.Fail("Should throw ArgumentException");
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.ToString().Contains("backgroundTokenCredentialRefreshInterval"));
            }

            try
            {
                new TokenCredentialCache(
                    new Mock<TokenCredential>().Object,
                    CosmosAuthorizationTests.AccountEndpoint,
                    backgroundTokenCredentialRefreshInterval: TimeSpan.MinValue,
                    tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);
                Assert.Fail("Should throw ArgumentException");
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.ToString().Contains("backgroundTokenCredentialRefreshInterval"));
            }

            try
            {
                new TokenCredentialCache(
                    new Mock<TokenCredential>().Object,
                    CosmosAuthorizationTests.AccountEndpoint,
                    backgroundTokenCredentialRefreshInterval: TimeSpan.Zero,
                    tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);
                Assert.Fail("Should throw ArgumentException");
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.ToString().Contains("backgroundTokenCredentialRefreshInterval"));
            }

            try
            {
                new TokenCredentialCache(
                    new Mock<TokenCredential>().Object,
                    CosmosAuthorizationTests.AccountEndpoint,
                    backgroundTokenCredentialRefreshInterval: TimeSpan.FromMilliseconds(-1),
                    tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);
                Assert.Fail("Should throw ArgumentException");
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.ToString().Contains("backgroundTokenCredentialRefreshInterval"));
            }

            // Which is roughly 24 days
            using TokenCredentialCache token = new TokenCredentialCache(
                    new Mock<TokenCredential>().Object,
                    CosmosAuthorizationTests.AccountEndpoint,
                    backgroundTokenCredentialRefreshInterval: TimeSpan.FromMilliseconds(Int32.MaxValue),
                    tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            using TokenCredentialCache disableBackgroundTask = new TokenCredentialCache(
                   new Mock<TokenCredential>().Object,
                   CosmosAuthorizationTests.AccountEndpoint,
                   backgroundTokenCredentialRefreshInterval: TimeSpan.MaxValue,
                   tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);
        }

        [TestMethod]
        public async Task TestTokenCredentialCacheHappyPathAsync()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(() => new ValueTask<AccessToken>(this.AccessToken));

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                await this.GetAndVerifyTokenAsync(tokenCredentialCache);
                this.ValidateSemaphoreIsReleased(tokenCredentialCache);
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialErrorAsync()
        {
            Exception exceptionToBeThrown = new Exception("Test Error Message");

            TestTokenCredential testTokenCredential = new TestTokenCredential(() => throw exceptionToBeThrown);

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                try
                {
                    await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
                    Assert.Fail("TokenCredentialCache.GetTokenAuthorizationHeaderAsync() is expected to fail but succeeded");
                }
                catch (Exception exception)
                {
                    // It should just throw the original exception and not be wrapped in a CosmosException.
                    // This avoids any confusion on where the error was thrown from.
                    Assert.IsTrue(object.ReferenceEquals(
                        exception,
                        exceptionToBeThrown));
                }

                // TokenCredential.GetTokenAuthorizationHeaderAsync() is retried for 3 times, so it should have been invoked for 4 times.
                Assert.AreEqual(2, testTokenCredential.NumTimesInvoked);
                this.ValidateSemaphoreIsReleased(tokenCredentialCache);
            }
        }

        [TestMethod]
        [TestCategory("Flaky")]
        [Timeout(30000)]
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
                    return new ValueTask<AccessToken>(new AccessToken(token2, DateTimeOffset.MaxValue));
                }
            });

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                string t1 = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
                Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token1), t1);

                // Token is valid for 6 seconds. Client TokenCredentialRefreshBuffer is set to 5 seconds.
                // After waiting for 2 seconds, the cache token is still valid, but it will be refreshed in the background.
                await Task.Delay(TimeSpan.FromSeconds(2));

                string t2 = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
                Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token1), t2);

                // Wait until the background refresh occurs.
                Stopwatch sw = Stopwatch.StartNew();
                while (testTokenCredential.NumTimesInvoked == 1)
                {
                    Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(20), "Background token refresh did not occur within 20 seconds.");
                    await Task.Delay(200);
                }

                string t3 = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
                Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token2), t3);

                Assert.AreEqual(2, testTokenCredential.NumTimesInvoked);
                this.ValidateSemaphoreIsReleased(tokenCredentialCache);
            }
        }

        // When disposing, the internal cancellationtoken should be signaled
        [TestMethod]
        [Timeout(30000)]
        public async Task TestTokenCredentialBackgroundRefreshAsync_OnDispose()
        {
            string token1 = "Token1";
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
                    throw new Exception("Should not call twice");
                }
            });

            TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential, TimeSpan.FromMilliseconds(100));
            string t1 = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
            this.ValidateSemaphoreIsReleased(tokenCredentialCache);

            tokenCredentialCache.Dispose();
            Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token1), t1);

            await Task.Delay(1000);
        }

        [TestMethod]
        public async Task TestTokenCredentialFailedToRefreshAsync()
        {
            string token = "Token";
            bool throwExceptionOnGetToken = false;
            Exception exception = new Exception();
            const int semaphoreCount = 10;
            using SemaphoreSlim semaphoreSlim = new SemaphoreSlim(semaphoreCount);
            TestTokenCredential testTokenCredential = new TestTokenCredential(async () =>
            {
                try
                {
                    await semaphoreSlim.WaitAsync();

                    Assert.AreEqual(semaphoreCount - 1, semaphoreSlim.CurrentCount, "Only a single refresh should occur at a time.");
                    if (throwExceptionOnGetToken)
                    {
                        throw exception;
                    }
                    else
                    {
                        return new AccessToken(token, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(8));
                    }
                }
                finally
                {
                    semaphoreSlim.Release();
                }
            });

            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");
            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token), await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace));
                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);
                throwExceptionOnGetToken = true;

                // Token is valid for 10 seconds. Client TokenCredentialRefreshBuffer is set to 5 seconds.
                // After waiting for 2 seconds, the cache token is still valid, but it will be refreshed in the background.
                await Task.Delay(TimeSpan.FromSeconds(2));
                Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token), await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace));
                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);

                // Token refreshes fails except for the first time, but the cached token will be served as long as it is valid.
                // Wait for the background refresh to occur. It should fail but the cached token should still be valid
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                while (testTokenCredential.NumTimesInvoked != 3)
                {
                    Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 10, "The background task did not start in 10 seconds");
                    await Task.Delay(200);
                }
                Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token), await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace));
                Assert.AreEqual(3, testTokenCredential.NumTimesInvoked, $"The cached token was not used. Waited time for background refresh: {stopwatch.Elapsed.TotalSeconds} seconds");

                // Cache token has expired, and it fails to refresh.
                await Task.Delay(TimeSpan.FromSeconds(5));
                throwExceptionOnGetToken = true;

                // Simulate multiple concurrent request on the failed token
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 40; i++)
                {
                    Task task = Task.Run(async () =>
                    {
                        try
                        {
                            await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace);
                            Assert.Fail("TokenCredentialCache.GetTokenAuthorizationHeaderAsync() is expected to fail but succeeded");
                        }
                        catch (Exception thrownException)
                        {
                            // It should just throw the original exception and not be wrapped in a CosmosException
                            // This avoids any confusion on where the error was thrown from.
                            Assert.IsTrue(object.ReferenceEquals(
                                exception,
                                thrownException), $"Incorrect exception thrown: Expected: {exception}; Actual: {thrownException}");
                        }
                    });
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

                this.ValidateSemaphoreIsReleased(tokenCredentialCache);


                // Simulate multiple concurrent request that should succeed after a failure
                throwExceptionOnGetToken = false;
                int numGetTokenCallsAfterFailures = testTokenCredential.NumTimesInvoked;
                tasks = new List<Task>();
                for (int i = 0; i < 40; i++)
                {
                    Task task = Task.Run(async () => await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace));
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

                this.ValidateSemaphoreIsReleased(tokenCredentialCache);
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialMultiThreadAsync()
        {
            // When multiple thread calls TokenCredentialCache.GetTokenAuthorizationHeaderAsync and a valid cached token
            // is not available, TokenCredentialCache will only create one task to get token.
            int numTasks = 100;
            bool delayTokenRefresh = true;
            TestTokenCredential testTokenCredential = new TestTokenCredential(async () =>
            {
                while (delayTokenRefresh)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }

                return this.AccessToken;
            });

            using (TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential))
            {
                Task[] tasks = new Task[numTasks];

                for (int i = 0; i < numTasks; i++)
                {
                    tasks[i] = Task.Run(() => this.GetAndVerifyTokenAsync(tokenCredentialCache));
                }

                bool waitForTasksToStart = false;
                do
                {
                    waitForTasksToStart = tasks.Where(x => x.Status == TaskStatus.Created).Any();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                } while (waitForTasksToStart);

                // Verify a task took the semaphore lock
                bool isRefreshing = false;
                do
                {
                    isRefreshing = this.IsTokenRefreshInProgress(tokenCredentialCache);
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                } while (!isRefreshing);

                delayTokenRefresh = false;

                await Task.WhenAll(tasks);

                this.ValidateSemaphoreIsReleased(tokenCredentialCache);
                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);
            }
        }

        [TestMethod]
        [DataRow("Bearer error=\"insufficient_claims\", claims=\"eyJhY2Nlc3NfdG9rZW4iOnt9fQ==\"", true, DisplayName = "With insufficient_claims")]
        [DataRow("Bearer claims=\"eyJhY2Nlc3NfdG9rZW4iOnt9fQ==\"", true, DisplayName = "With claims only")]
        [DataRow("Bearer realm=\"test\"", false, DisplayName = "Without claims challenge")]
        [DataRow("", false, DisplayName = "Empty header")]
        public void TryHandleTokenRevocation_VariousHeaders(string wwwAuthenticateValue, bool expectedResult)
        {
            // Arrange
            Mock<TokenCredential> mockTokenCredential = new Mock<TokenCredential>();
            mockTokenCredential
                .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccessToken("test-token", DateTimeOffset.MaxValue));

            using AuthorizationTokenProviderTokenCredential tokenProvider = new AuthorizationTokenProviderTokenCredential(
                mockTokenCredential.Object,
                CosmosAuthorizationTests.AccountEndpoint,
                backgroundTokenCredentialRefreshInterval: TimeSpan.FromMinutes(5),
                tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            // Act
            bool result = tokenProvider.TryHandleTokenRevocation(HttpStatusCode.Unauthorized, wwwAuthenticateValue);

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.NotFound)]
        public void TryHandleTokenRevocation_NonUnauthorizedStatus_ReturnsFalse(HttpStatusCode statusCode)
        {
            // Arrange
            Mock<TokenCredential> mockTokenCredential = new Mock<TokenCredential>();
            mockTokenCredential
                .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccessToken("test-token", DateTimeOffset.MaxValue));

            using AuthorizationTokenProviderTokenCredential tokenProvider = new AuthorizationTokenProviderTokenCredential(
                mockTokenCredential.Object,
                CosmosAuthorizationTests.AccountEndpoint,
                backgroundTokenCredentialRefreshInterval: TimeSpan.FromMinutes(5),
                tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            string wwwAuthenticateValue = "Bearer error=\"insufficient_claims\", claims=\"eyJhY2Nlc3NfdG9rZW4iOnt9fQ==\"";

            // Act
            bool result = tokenProvider.TryHandleTokenRevocation(statusCode, wwwAuthenticateValue);
            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null claims")]
        [DataRow("", DisplayName = "Empty claims")]
        public void MergeClaimsWithClientCapabilities_NullOrEmpty_ReturnsNull(string claimsChallenge)
        {
            // No revocation / CAE challenge outstanding => no claims must be attached, so the
            // credential's own token cache stays usable (a non-empty 'claims' forces MSAL to
            // bypass its cache and go live to ESTS on every acquisition).
            // Act
            string result = TokenCredentialCache.MergeClaimsWithClientCapabilities(claimsChallenge);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [DataRow("not-valid-base64!!!", "{\"access_token\":{\"xms_cc\":{\"values\":[\"cp1\"]}}}", DisplayName = "Invalid base64")]
        public void MergeClaimsWithClientCapabilities_InvalidChallenge_ReturnsOnlyCp1(string claimsChallenge, string expected)
        {
            // A non-empty but malformed challenge still means a challenge occurred, so we fall
            // back to attaching only the cp1 client capability.
            // Act
            string result = TokenCredentialCache.MergeClaimsWithClientCapabilities(claimsChallenge);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MergeClaimsWithClientCapabilities_ValidClaims_MergesWithCp1()
        {
            // Arrange - Base64 encoded: {"access_token":{"acrs":{"essential":true,"value":"c1"}}}
            string claimsChallenge = "eyJhY2Nlc3NfdG9rZW4iOnsiYWNycyI6eyJlc3NlbnRpYWwiOnRydWUsInZhbHVlIjoiYzEifX19";

            // Act
            string result = TokenCredentialCache.MergeClaimsWithClientCapabilities(claimsChallenge);

            // Assert
            Assert.IsTrue(result.Contains("\"xms_cc\":{\"values\":[\"cp1\"]}"), "Should contain cp1");
            Assert.IsTrue(result.Contains("\"acrs\""), "Should contain original claims");
        }

        [DataTestMethod]
        // {"access_token":{}}
        [DataRow("eyJhY2Nlc3NfdG9rZW4iOnt9fQ==", DisplayName = "Empty access_token object")]
        // {"access_token":{"nbf":{"essential":true,"value":"1"}}} - real revocation shape
        [DataRow("eyJhY2Nlc3NfdG9rZW4iOnsibmJmIjp7ImVzc2VudGlhbCI6dHJ1ZSwidmFsdWUiOiIxIn19fQ==", DisplayName = "nbf revocation shape")]
        // {"access_token":{"nonce":"a}b"}} - brace inside a string value
        [DataRow("eyJhY2Nlc3NfdG9rZW4iOnsibm9uY2UiOiJhfWIifX0=", DisplayName = "Brace inside string value")]
        // {"access_token":{"xms_cc":{"values":["cp2"]},"nbf":{"value":"1"}}} - pre-existing xms_cc must be overwritten
        [DataRow("eyJhY2Nlc3NfdG9rZW4iOnsieG1zX2NjIjp7InZhbHVlcyI6WyJjcDIiXX0sIm5iZiI6eyJ2YWx1ZSI6IjEifX19", DisplayName = "Pre-existing xms_cc is overwritten")]
        public void MergeClaimsWithClientCapabilities_ProducesValidJson(string base64Claims)
        {
            // Act
            string merged = TokenCredentialCache.MergeClaimsWithClientCapabilities(base64Claims);

            // Assert - result must be parseable JSON with the expected shape.
            using JsonDocument doc = JsonDocument.Parse(merged);
            JsonElement accessToken = doc.RootElement.GetProperty("access_token");
            Assert.AreEqual(JsonValueKind.Object, accessToken.ValueKind);

            JsonElement xmsCc = accessToken.GetProperty("xms_cc");
            JsonElement values = xmsCc.GetProperty("values");
            Assert.AreEqual(1, values.GetArrayLength());
            Assert.AreEqual("cp1", values[0].GetString());
        }

        [TestMethod]
        public async Task TokenCredentialCache_ResetWithClaims_RefreshesTokenWithClaims()
        {
            // Arrange
            int callCount = 0;
            List<string> claimsReceived = new List<string>();

            TestTokenCredential testTokenCredential = new TestTokenCredential(() =>
            {
                callCount++;
                return new ValueTask<AccessToken>(new AccessToken($"Token{callCount}", DateTimeOffset.MaxValue));
            });

            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential);

            // Get initial token
            string t1 = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
            Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("Token1"), t1);
            Assert.AreEqual(1, callCount);

            // Simulate CAE revocation with claims
            string claimsChallenge = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("{\"access_token\":{\"acrs\":{\"essential\":true,\"value\":\"c1\"}}}"));
            tokenCredentialCache.ResetCachedToken(claimsChallenge);

            // Get token again
            string t2 = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);

            // Assert
            Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("Token2"), t2);
            Assert.AreEqual(2, callCount);
        }

        [TestMethod]
        public async Task TokenCredentialCache_ResetWithNullClaims_RefreshesToken()
        {
            // Arrange
            int callCount = 0;
            TestTokenCredential testTokenCredential = new TestTokenCredential(() =>
            {
                callCount++;
                return new ValueTask<AccessToken>(new AccessToken($"Token{callCount}", DateTimeOffset.MaxValue));
            });

            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential);

            // Get initial token
            await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
            Assert.AreEqual(1, callCount);

            // Reset with null claims
            tokenCredentialCache.ResetCachedToken(claimsChallenge: null);

            // Get token again
            await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);

            // Assert
            Assert.AreEqual(2, callCount);
        }

        // ---------------------------------------------------------------------------------------
        // Regression tests for the AAD ReadAccountAsync hang introduced by PR #5549 (CAE / token
        // revocation) between SDK 3.61.0 and main.
        //
        // Root cause: TokenCredentialCache attached a NON-EMPTY 'claims' parameter (the cp1 client
        // capability) on EVERY token acquisition — even when there was no revocation challenge —
        // because MergeClaimsWithClientCapabilities(null) returns the cp1 JSON rather than null.
        // Azure.Identity / MSAL treat any non-empty 'claims' as "the cached token does not satisfy
        // this challenge" and therefore SKIP AcquireTokenSilent's token cache and go live to ESTS on
        // every acquisition. Under an MSAL-backed credential (certificate / managed identity) that
        // live call can stall, and because all callers funnel through a single-flight refresh, the
        // first ReadAccountAsync (and everything after it) hangs. cp1/CAE is ALREADY advertised the
        // correct, cache-friendly way via isCaeEnabled:true in CosmosScopeProvider, so the claims
        // injection was redundant. The fix attaches 'claims' only on an actual revocation challenge.
        // ---------------------------------------------------------------------------------------

        [TestMethod]
        public async Task TokenCredentialCache_NormalAcquisition_DoesNotAttachClaims_SoMsalCacheIsUsable()
        {
            // Arrange
            ClaimsCapturingTokenCredential credential = new ClaimsCapturingTokenCredential();
            using TokenCredentialCache cache = this.CreateTokenCredentialCache(credential, TimeSpan.FromMinutes(30));

            // Act — a normal first acquisition with no revocation challenge outstanding.
            await cache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);

            // Assert
            Assert.AreEqual(1, credential.Requests.Count, "Expected exactly one token acquisition.");
            (string claims, bool isCaeEnabled) = credential.Requests[0];

            Assert.IsTrue(
                string.IsNullOrEmpty(claims),
                "REGRESSION (PR #5549): the normal (no-revocation) token acquisition must NOT attach a " +
                "'claims' parameter. A non-empty claims forces Azure.Identity/MSAL to bypass its token " +
                "cache (AcquireTokenSilent) and call ESTS live on every acquisition, which stalls " +
                $"ReadAccountAsync under MSAL-backed credentials. Actual claims sent: '{claims}'.");

            Assert.IsTrue(
                isCaeEnabled,
                "CAE / cp1 must still be advertised the cache-friendly way, via isCaeEnabled:true.");
        }

        [TestMethod]
        public async Task TokenCredentialCache_RevocationChallenge_StillAttachesMergedClaims()
        {
            // Arrange
            ClaimsCapturingTokenCredential credential = new ClaimsCapturingTokenCredential();
            using TokenCredentialCache cache = this.CreateTokenCredentialCache(credential, TimeSpan.FromMinutes(30));

            // Act — normal acquire, then simulate a CAE revocation challenge and acquire again.
            await cache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);

            string claimsChallenge = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("{\"access_token\":{\"acrs\":{\"essential\":true,\"value\":\"c1\"}}}"));
            cache.ResetCachedToken(claimsChallenge);

            await cache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);

            // Assert
            Assert.AreEqual(2, credential.Requests.Count, "Expected two token acquisitions.");
            Assert.IsTrue(
                string.IsNullOrEmpty(credential.Requests[0].Claims),
                "The first (normal) acquisition must not attach claims.");

            string revocationClaims = credential.Requests[1].Claims;
                Assert.IsTrue(revocationClaims != null && revocationClaims.Length > 0,
                "On an actual revocation challenge, the SDK MUST attach the claims so CAE revocation works.");
            Assert.IsTrue(revocationClaims.Contains("acrs"), "Revocation claims must include the challenge's claims.");
            Assert.IsTrue(revocationClaims.Contains("cp1"), "Revocation claims must still include the cp1 client capability.");
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task TokenCredentialCache_NormalAcquisition_DoesNotStallOnMsalCacheBypass()
        {
            // This is the deterministic reproduction of the customer-reported hang. The credential
            // faithfully models MSAL's behavior: a non-empty 'claims' cannot be served from the token
            // cache and triggers a live (here: long-stalling) ESTS call, whereas an empty 'claims' is
            // served fast. On the normal path the SDK must send NO claims, so the acquisition must NOT
            // stall. Before the fix, the SDK sent cp1 claims here -> the credential stalls -> the
            // acquisition (and thus ReadAccountAsync) hangs, and this test times out.
            TimeSpan stallWhenCacheBypassed = TimeSpan.FromSeconds(20);
            ClaimsCapturingTokenCredential credential = new ClaimsCapturingTokenCredential(
                delayWhenClaimsPresent: stallWhenCacheBypassed);
            using TokenCredentialCache cache = this.CreateTokenCredentialCache(credential, TimeSpan.FromMinutes(30));

            // Act
            Task<string> acquire = cache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton).AsTask();
            Task first = await Task.WhenAny(acquire, Task.Delay(TimeSpan.FromSeconds(5)));

            // Assert
            Assert.AreSame(
                acquire,
                first,
                "REGRESSION (PR #5549): the first token acquisition (no revocation challenge) stalled. " +
                "The SDK attached a non-empty 'claims' on the normal path, forcing the MSAL cache-bypass " +
                "live path and hanging ReadAccountAsync. With the fix (no claims on the normal path) the " +
                "acquisition completes immediately.");

            string header = await acquire;
            Assert.IsFalse(string.IsNullOrEmpty(header));
        }

        /// <summary>
        /// A TokenCredential that records the claims / IsCaeEnabled of every request context the SDK
        /// passes, and — to model Azure.Identity/MSAL cache-bypass semantics — optionally stalls when a
        /// non-empty <c>claims</c> is present (a claims challenge cannot be served from the silent cache
        /// and forces a live ESTS call), while returning immediately when no claims are present.
        /// </summary>
        private sealed class ClaimsCapturingTokenCredential : TokenCredential
        {
            private readonly TimeSpan delayWhenClaimsPresent;

            public ClaimsCapturingTokenCredential(TimeSpan? delayWhenClaimsPresent = null)
            {
                this.delayWhenClaimsPresent = delayWhenClaimsPresent ?? TimeSpan.Zero;
            }

            public List<(string Claims, bool IsCaeEnabled)> Requests { get; } = new List<(string, bool)>();

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return this.GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
            }

            public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                bool hasClaims = !string.IsNullOrEmpty(requestContext.Claims);
                lock (this.Requests)
                {
                    this.Requests.Add((requestContext.Claims, requestContext.IsCaeEnabled));
                }

                if (hasClaims && this.delayWhenClaimsPresent > TimeSpan.Zero)
                {
                    // Models MSAL: a non-empty claims challenge skips the token cache and goes live.
                    await Task.Delay(this.delayWhenClaimsPresent, cancellationToken);
                }

                return new AccessToken("AccessToken", DateTimeOffset.MaxValue);
            }
        }

        [TestMethod]
        public async Task TokenCredentialCache_ThrowsObjectDisposed_AfterDispose()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(() => new ValueTask<AccessToken>(this.AccessToken));
            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential, TimeSpan.MaxValue);

            tokenCredentialCache.Dispose();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton).AsTask());
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.Forbidden)]
        public async Task TokenCredentialCache_DoesNotRetry_OnUnauthorizedOrForbidden(HttpStatusCode statusCode)
        {
            int callCount = 0;
            Mock<TokenCredential> mockCredential = new Mock<TokenCredential>();
            mockCredential
                .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .Returns<TokenRequestContext, CancellationToken>((ctx, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new global::Azure.RequestFailedException((int)statusCode, "authorization failure");
                    }

                    return new ValueTask<AccessToken>(new AccessToken("recovered-token", DateTimeOffset.UtcNow.AddHours(1)));
                });

            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(mockCredential.Object, TimeSpan.MaxValue);
            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");

            global::Azure.RequestFailedException thrown = await Assert.ThrowsExceptionAsync<global::Azure.RequestFailedException>(
                () => tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace).AsTask());

            Assert.AreEqual((int)statusCode, thrown.Status);
            Assert.AreEqual(1, callCount, "A 401/403 from the credential must not be retried within a single refresh.");

            // The cached auth state is cleared on 401/403, so the next request re-invokes the credential and recovers.
            string header = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace);
            Assert.AreEqual(AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("recovered-token"), header);
            Assert.AreEqual(2, callCount);
        }

        [TestMethod]
        public async Task TokenCredentialCache_Throws_WhenCredentialReturnsExpiredToken()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(
                () => new ValueTask<AccessToken>(new AccessToken("expired-token", DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5))));

            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential, TimeSpan.MaxValue);
            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");

            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace).AsTask());

            // The expired-token guard sits inside the retry loop (totalRetryCount = 2 iterations),
            // so the credential is invoked on the initial attempt plus one retry = 2 total invocations.
            Assert.AreEqual(2, testTokenCredential.NumTimesInvoked);
        }

        [TestMethod]
        public async Task TokenCredentialCache_MapsCancellation_ToFailedToGetAadTokenSubStatus()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(
                () => throw new OperationCanceledException("token fetch cancelled"));

            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential, TimeSpan.MaxValue);
            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");

            CosmosException ce = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace).AsTask());

            Assert.AreEqual(HttpStatusCode.RequestTimeout, ce.StatusCode);
            Assert.AreEqual((int)SubStatusCodes.FailedToGetAadToken, ce.SubStatusCode);
        }

        [TestMethod]
        public void GenerateAadAuthorizationSignature_ProducesExpectedFormat()
        {
            string token = "sample.jwt.token";

            string signature = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(token);

            // The header is URL-encoded; decoding it back must yield the documented AAD signature structure.
            string decoded = Uri.UnescapeDataString(signature);
            Assert.AreEqual($"type=aad&ver=1.0&sig={token}", decoded);
        }

        [TestMethod]
        public async Task AddAuthorizationHeaderAsync_AddsAuthorizationHeader()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(() => new ValueTask<AccessToken>(this.AccessToken));
            using AuthorizationTokenProvider provider = new AuthorizationTokenProviderTokenCredential(
                testTokenCredential,
                CosmosAuthorizationTests.AccountEndpoint,
                backgroundTokenCredentialRefreshInterval: TimeSpan.MaxValue,
                AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);

            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            await provider.AddAuthorizationHeaderAsync(
                headers,
                CosmosAuthorizationTests.AccountEndpoint,
                "GET",
                AuthorizationTokenType.PrimaryMasterKey);

            string authorizationHeader = headers[HttpConstants.HttpHeaders.Authorization];
            Assert.IsFalse(string.IsNullOrEmpty(authorizationHeader), "Authorization header should be populated.");
            Assert.AreEqual(
                AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(this.AccessToken.Token),
                authorizationHeader);
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task TokenCredentialCache_MaxValueInterval_DisablesBackgroundRefresh()
        {
            TestTokenCredential testTokenCredential = new TestTokenCredential(
                () => new ValueTask<AccessToken>(new AccessToken("token", DateTimeOffset.MaxValue)));

            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential, TimeSpan.MaxValue);
            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");

            await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace);
            Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);

            // With TimeSpan.MaxValue the background refresh loop must exit immediately without ever
            // refreshing. Poll over a bounded window and assert the credential is never invoked again;
            // polling also catches an erroneous refresh that fires partway through the window, which a
            // single end-of-wait check would miss.
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(3))
            {
                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked, "TimeSpan.MaxValue should disable the background refresh loop.");
                await Task.Delay(200);
            }
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task TokenCredentialCache_RefreshesToken_WhenCachedTokenIsExpired()
        {
            // The first call returns a token that is valid at fetch time but expires shortly after.
            // This forces the following request to miss the cache and re-acquire a token on the request
            // path (the on-demand refresh branch in GetTokenAuthorizationHeaderAsync), independent of the
            // background refresh loop.
            int invocation = 0;
            TestTokenCredential testTokenCredential = new TestTokenCredential(() =>
            {
                invocation++;
                return invocation == 1
                    ? new ValueTask<AccessToken>(new AccessToken("short-lived-token", DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2)))
                    : new ValueTask<AccessToken>(new AccessToken("refreshed-token", DateTimeOffset.UtcNow + TimeSpan.FromHours(1)));
            });

            // TimeSpan.MaxValue disables the background refresh loop, isolating the on-demand refresh path.
            using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(testTokenCredential, TimeSpan.MaxValue);
            using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");

            string firstHeader = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace);
            Assert.AreEqual(
                AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("short-lived-token"),
                firstHeader);
            Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);

            // Wait until the cached token is past its expiry.
            await Task.Delay(TimeSpan.FromSeconds(3));

            // The cached token is now expired, so the request path must acquire a fresh token rather than
            // serve the stale cached header.
            string secondHeader = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace);
            Assert.AreEqual(
                AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("refreshed-token"),
                secondHeader);
            Assert.AreEqual(2, testTokenCredential.NumTimesInvoked);
        }

        [TestMethod]
        [DoNotParallelize]
        public async Task TokenCredentialCache_UsesFallbackScope_WithinRetryLoop_AfterAadsts500011()
        {
            string previous = Environment.GetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE");
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", null);

            try
            {
                const string accountScope = "https://test-account.documents.azure.com/.default";
                const string fallbackScope = "https://cosmos.azure.com/.default";
                List<string> requestedScopes = new List<string>();

                Mock<TokenCredential> mockCredential = new Mock<TokenCredential>();
                mockCredential
                    .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                    .Returns<TokenRequestContext, CancellationToken>((ctx, ct) =>
                    {
                        string scope = ctx.Scopes[0];
                        requestedScopes.Add(scope);

                        if (string.Equals(scope, accountScope, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new global::Azure.RequestFailedException("AADSTS500011: The resource principal named was not found.");
                        }

                        return new ValueTask<AccessToken>(new AccessToken("fallback-token", DateTimeOffset.UtcNow.AddHours(1)));
                    });

                using TokenCredentialCache tokenCredentialCache = this.CreateTokenCredentialCache(mockCredential.Object, TimeSpan.MaxValue);
                using ITrace trace = Cosmos.Tracing.Trace.GetRootTrace("test");

                string header = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(trace);

                Assert.AreEqual(
                    AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature("fallback-token"),
                    header);
                Assert.AreEqual(2, requestedScopes.Count, "Expected an account-scope attempt followed by a fallback-scope retry.");
                Assert.AreEqual(accountScope, requestedScopes[0]);
                Assert.AreEqual(fallbackScope, requestedScopes[1]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", previous);
            }
        }

        private TokenCredentialCache CreateTokenCredentialCache(
            TokenCredential tokenCredential)
        {
            return this.CreateTokenCredentialCache(tokenCredential, TimeSpan.FromSeconds(5));
        }

        private TokenCredentialCache CreateTokenCredentialCache(
            TokenCredential tokenCredential,
            TimeSpan refreshInterval)
        {
            return new TokenCredentialCache(
                tokenCredential,
                CosmosAuthorizationTests.AccountEndpoint,
                backgroundTokenCredentialRefreshInterval: refreshInterval,
                tokenToAuthorizationHeader: AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature);
        }

        private bool IsTokenRefreshInProgress(TokenCredentialCache tokenCredentialCache)
        {
            Type type = typeof(TokenCredentialCache);
            FieldInfo sempahoreFieldInfo = type.GetField("currentRefreshOperation", BindingFlags.NonPublic | BindingFlags.Instance);
            Task refreshToken = (Task)sempahoreFieldInfo.GetValue(tokenCredentialCache);
            return refreshToken != null;
        }

        private int GetSemaphoreCurrentCount(TokenCredentialCache tokenCredentialCache)
        {
            Type type = typeof(TokenCredentialCache);
            FieldInfo sempahoreFieldInfo = type.GetField("isTokenRefreshingLock", BindingFlags.NonPublic | BindingFlags.Instance);
            SemaphoreSlim semaphoreSlim = (SemaphoreSlim)sempahoreFieldInfo.GetValue(tokenCredentialCache);
            return semaphoreSlim.CurrentCount;
        }

        private void ValidateSemaphoreIsReleased(TokenCredentialCache tokenCredentialCache)
        {
            int currentCount = this.GetSemaphoreCurrentCount(tokenCredentialCache);
            Assert.AreEqual(1, currentCount);
        }

        private async Task GetAndVerifyTokenAsync(TokenCredentialCache tokenCredentialCache)
        {
            string result = await tokenCredentialCache.GetTokenAuthorizationHeaderAsync(NoOpTrace.Singleton);
            string expectedAuthorizationHeader = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(this.AccessToken.Token);
            Assert.AreEqual(
                expectedAuthorizationHeader,
                result);
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
                Assert.AreEqual(CosmosAuthorizationTests.ExpectedScope, requestContext.Scopes[0]);

                return this.accessTokenFunc().GetAwaiter().GetResult();
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.NumTimesInvoked++;

                Assert.AreEqual(1, requestContext.Scopes.Length);
                Assert.AreEqual(CosmosAuthorizationTests.ExpectedScope, requestContext.Scopes[0]);

                return this.accessTokenFunc();
            }
        }
    }
}