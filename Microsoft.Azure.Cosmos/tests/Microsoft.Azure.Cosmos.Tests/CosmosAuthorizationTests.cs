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
                "VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=",
                defaultDateTime: new DateTime(2030, 9, 21, 9, 9, 9, DateTimeKind.Utc));

            using AuthorizationTokenProvider cosmosAuthorization = new AuthorizationTokenProviderTokenCredential(
                simpleEmulatorTokenCredential,
                new Uri("https://127.0.0.1:8081"),
                backgroundTokenCredentialRefreshInterval: TimeSpan.FromSeconds(1));

            {
                StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
                (string token, string payload) = await cosmosAuthorization.GetUserAuthorizationAsync(
                    "dbs\\test",
                    ResourceType.Database.ToResourceTypeString(),
                    "GET",
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                Assert.AreEqual(
                    "type%3daad%26ver%3d1.0%26sig%3dew0KICAgICAgICAgICAgICAgICJhbGciOiJSUzI1NiIsDQogICAgICAgICAgICAgICAgImtpZCI6InhfOUtTdXNLVTVZY0hmNCIsDQogICAgICAgICAgICAgICAgInR5cCI6IkpXVCINCiAgICAgICAgICAgIH0.ew0KICAgICAgICAgICAgICAgICJvaWQiOiI5NjMxMzAzNC00NzM5LTQzY2ItOTNjZC03NDE5M2FkYmU1YjYiLA0KICAgICAgICAgICAgICAgICJ0aWQiOiI3YjE5OTlhMS1kZmQ3LTQ0MGUtODIwNC0wMDE3MDk3OWI5ODQiLA0KICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLA0KICAgICAgICAgICAgICAgICJncm91cHMiOlsNCiAgICAgICAgICAgICAgICAgICAgIjdjZTFkMDAzLTRjYjMtNDg3OS1iN2M1LTc0MDYyYTM1YzY2ZSIsDQogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLA0KICAgICAgICAgICAgICAgICAgICAiNTU0OWJiNjItYzc3Yi00MzA1LWJkYTktOWVjNjZiODVkOWU0IiwNCiAgICAgICAgICAgICAgICAgICAgImM0NGZkNjg1LTVjNTgtNDUyYy1hYWY3LTEzY2U3NTE4NGY2NSIsDQogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiDQogICAgICAgICAgICAgICAgXSwNCiAgICAgICAgICAgICAgICAibmJmIjoxOTE2MjEyMTQ5LA0KICAgICAgICAgICAgICAgICJleHAiOjE5MTYyMTU3NDksDQogICAgICAgICAgICAgICAgImlhdCI6MTU5NjU5MjMzNSwNCiAgICAgICAgICAgICAgICAiaXNzIjoiaHR0cHM6Ly9zdHMuZmFrZS1pc3N1ZXIubmV0LzdiMTk5OWExLWRmZDctNDQwZS04MjA0LTAwMTcwOTc5Yjk4NCIsDQogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCINCiAgICAgICAgICAgIH0.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0",
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
                    "type%3daad%26ver%3d1.0%26sig%3dew0KICAgICAgICAgICAgICAgICJhbGciOiJSUzI1NiIsDQogICAgICAgICAgICAgICAgImtpZCI6InhfOUtTdXNLVTVZY0hmNCIsDQogICAgICAgICAgICAgICAgInR5cCI6IkpXVCINCiAgICAgICAgICAgIH0.ew0KICAgICAgICAgICAgICAgICJvaWQiOiI5NjMxMzAzNC00NzM5LTQzY2ItOTNjZC03NDE5M2FkYmU1YjYiLA0KICAgICAgICAgICAgICAgICJ0aWQiOiI3YjE5OTlhMS1kZmQ3LTQ0MGUtODIwNC0wMDE3MDk3OWI5ODQiLA0KICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLA0KICAgICAgICAgICAgICAgICJncm91cHMiOlsNCiAgICAgICAgICAgICAgICAgICAgIjdjZTFkMDAzLTRjYjMtNDg3OS1iN2M1LTc0MDYyYTM1YzY2ZSIsDQogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLA0KICAgICAgICAgICAgICAgICAgICAiNTU0OWJiNjItYzc3Yi00MzA1LWJkYTktOWVjNjZiODVkOWU0IiwNCiAgICAgICAgICAgICAgICAgICAgImM0NGZkNjg1LTVjNTgtNDUyYy1hYWY3LTEzY2U3NTE4NGY2NSIsDQogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiDQogICAgICAgICAgICAgICAgXSwNCiAgICAgICAgICAgICAgICAibmJmIjoxOTE2MjEyMTQ5LA0KICAgICAgICAgICAgICAgICJleHAiOjE5MTYyMTU3NDksDQogICAgICAgICAgICAgICAgImlhdCI6MTU5NjU5MjMzNSwNCiAgICAgICAgICAgICAgICAiaXNzIjoiaHR0cHM6Ly9zdHMuZmFrZS1pc3N1ZXIubmV0LzdiMTk5OWExLWRmZDctNDQwZS04MjA0LTAwMTcwOTc5Yjk4NCIsDQogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCINCiAgICAgICAgICAgIH0.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0", token);
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
                    "type%3daad%26ver%3d1.0%26sig%3dew0KICAgICAgICAgICAgICAgICJhbGciOiJSUzI1NiIsDQogICAgICAgICAgICAgICAgImtpZCI6InhfOUtTdXNLVTVZY0hmNCIsDQogICAgICAgICAgICAgICAgInR5cCI6IkpXVCINCiAgICAgICAgICAgIH0.ew0KICAgICAgICAgICAgICAgICJvaWQiOiI5NjMxMzAzNC00NzM5LTQzY2ItOTNjZC03NDE5M2FkYmU1YjYiLA0KICAgICAgICAgICAgICAgICJ0aWQiOiI3YjE5OTlhMS1kZmQ3LTQ0MGUtODIwNC0wMDE3MDk3OWI5ODQiLA0KICAgICAgICAgICAgICAgICJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLA0KICAgICAgICAgICAgICAgICJncm91cHMiOlsNCiAgICAgICAgICAgICAgICAgICAgIjdjZTFkMDAzLTRjYjMtNDg3OS1iN2M1LTc0MDYyYTM1YzY2ZSIsDQogICAgICAgICAgICAgICAgICAgICJlOTlmZjMwYy1jMjI5LTRjNjctYWIyOS0zMGE2YWViYzNlNTgiLA0KICAgICAgICAgICAgICAgICAgICAiNTU0OWJiNjItYzc3Yi00MzA1LWJkYTktOWVjNjZiODVkOWU0IiwNCiAgICAgICAgICAgICAgICAgICAgImM0NGZkNjg1LTVjNTgtNDUyYy1hYWY3LTEzY2U3NTE4NGY2NSIsDQogICAgICAgICAgICAgICAgICAgICJiZTg5NTIxNS1lYWI1LTQzYjctOTUzNi05ZWY4ZmUxMzAzMzAiDQogICAgICAgICAgICAgICAgXSwNCiAgICAgICAgICAgICAgICAibmJmIjoxOTE2MjEyMTQ5LA0KICAgICAgICAgICAgICAgICJleHAiOjE5MTYyMTU3NDksDQogICAgICAgICAgICAgICAgImlhdCI6MTU5NjU5MjMzNSwNCiAgICAgICAgICAgICAgICAiaXNzIjoiaHR0cHM6Ly9zdHMuZmFrZS1pc3N1ZXIubmV0LzdiMTk5OWExLWRmZDctNDQwZS04MjA0LTAwMTcwOTc5Yjk4NCIsDQogICAgICAgICAgICAgICAgImF1ZCI6Imh0dHBzOi8vbG9jYWxob3N0LmxvY2FsaG9zdCINCiAgICAgICAgICAgIH0.VkdocGN5QnBjeUJoSUhOaGJYQnNaU0J6ZEhKcGJtYz0", token);
                Assert.IsNull(payload);
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
                    backgroundTokenCredentialRefreshInterval: toLarge);
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
                    backgroundTokenCredentialRefreshInterval: TimeSpan.MinValue);
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
                    backgroundTokenCredentialRefreshInterval: TimeSpan.Zero);
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
                    backgroundTokenCredentialRefreshInterval: TimeSpan.FromMilliseconds(-1));
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
                    backgroundTokenCredentialRefreshInterval: TimeSpan.FromMilliseconds(Int32.MaxValue));

            using TokenCredentialCache disableBackgroundTask = new TokenCredentialCache(
                   new Mock<TokenCredential>().Object,
                   CosmosAuthorizationTests.AccountEndpoint,
                   backgroundTokenCredentialRefreshInterval: TimeSpan.MaxValue);
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
                    await tokenCredentialCache.GetTokenAsync(NoOpTrace.Singleton);
                    Assert.Fail("TokenCredentialCache.GetTokenAsync() is expected to fail but succeeded");
                }
                catch (Exception exception)
                {
                    // It should just throw the original exception and not be wrapped in a CosmosException.
                    // This avoids any confusion on where the error was thrown from.
                    Assert.IsTrue(object.ReferenceEquals(
                        exception,
                        exceptionToBeThrown));
                }

                // TokenCredential.GetTokenAsync() is retried for 3 times, so it should have been invoked for 4 times.
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
                string t1 = await tokenCredentialCache.GetTokenAsync(NoOpTrace.Singleton);
                Assert.AreEqual(token1, t1);

                // Token is valid for 6 seconds. Client TokenCredentialRefreshBuffer is set to 5 seconds.
                // After waiting for 2 seconds, the cache token is still valid, but it will be refreshed in the background.
                await Task.Delay(TimeSpan.FromSeconds(2));

                string t2 = await tokenCredentialCache.GetTokenAsync(NoOpTrace.Singleton);
                Assert.AreEqual(token1, t2);

                // Wait until the background refresh occurs.
                while (testTokenCredential.NumTimesInvoked == 1)
                {
                    await Task.Delay(500);
                }

                string t3 = await tokenCredentialCache.GetTokenAsync(NoOpTrace.Singleton);
                Assert.AreEqual(token2, t3);

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
            string t1 = await tokenCredentialCache.GetTokenAsync(NoOpTrace.Singleton);
            this.ValidateSemaphoreIsReleased(tokenCredentialCache);

            tokenCredentialCache.Dispose();
            Assert.AreEqual(token1, t1);

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
                Assert.AreEqual(token, await tokenCredentialCache.GetTokenAsync(trace));
                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);
                throwExceptionOnGetToken = true;

                // Token is valid for 10 seconds. Client TokenCredentialRefreshBuffer is set to 5 seconds.
                // After waiting for 2 seconds, the cache token is still valid, but it will be refreshed in the background.
                await Task.Delay(TimeSpan.FromSeconds(2));
                Assert.AreEqual(token, await tokenCredentialCache.GetTokenAsync(trace));
                Assert.AreEqual(1, testTokenCredential.NumTimesInvoked);

                // Token refreshes fails except for the first time, but the cached token will be served as long as it is valid.
                // Wait for the background refresh to occur. It should fail but the cached token should still be valid
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                while (testTokenCredential.NumTimesInvoked != 3)
                {
                    Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 10, "The background task did not start in 10 seconds");
                    await Task.Delay(200);
                }
                Assert.AreEqual(token, await tokenCredentialCache.GetTokenAsync(trace));
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
                            await tokenCredentialCache.GetTokenAsync(trace);
                            Assert.Fail("TokenCredentialCache.GetTokenAsync() is expected to fail but succeeded");
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
                    Task task = Task.Run(async () => await tokenCredentialCache.GetTokenAsync(trace));
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

                this.ValidateSemaphoreIsReleased(tokenCredentialCache);
            }
        }

        [TestMethod]
        public async Task TestTokenCredentialMultiThreadAsync()
        {
            // When multiple thread calls TokenCredentialCache.GetTokenAsync and a valid cached token
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
                backgroundTokenCredentialRefreshInterval: refreshInterval);
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
            string result = await tokenCredentialCache.GetTokenAsync(NoOpTrace.Singleton);
            Assert.AreEqual(
                this.AccessToken.Token,
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