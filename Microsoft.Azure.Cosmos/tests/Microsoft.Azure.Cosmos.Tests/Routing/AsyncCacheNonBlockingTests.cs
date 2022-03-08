//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;


    [TestClass]
    public class AsyncCacheNonBlockingTests
    {
       

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        [ExpectedException(typeof(NotFoundException))]
        public async Task ValidateNegativeScenario(bool forceRefresh)
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new NotFoundException("testNotFoundException");
                },
                forceRefresh,
                null);
        }

        [TestMethod]
        public async Task ValidateMultipleBackgroundRefreshesScenario()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            
            string expectedValue = "ResponseValue";
            string response = await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return expectedValue;
                },
                false,
                null);

            Assert.AreEqual(expectedValue, response);

            for (int i = 0; i < 10; i++)
            {
                string forceRefreshResponse = await asyncCache.GetAsync(
                    key: "test",
                    singleValueInitFunc: async (staleValue) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(5));
                        return expectedValue+i;
                    },
                    forceRefresh: true,
                    callBackOnForceRefresh: null);

                Assert.AreEqual(expectedValue+i, forceRefreshResponse);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(NotFoundException))]
        public async Task ValidateNegativeNotAwaitedScenario()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            Task task1 = asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new NotFoundException("testNotFoundException");
                },
                false,
                null);

            try
            {
                await asyncCache.GetAsync(
                    "test",
                    (staleValue) => throw new BadRequestException("testBadRequestException"),
                    false,
                    null);
                Assert.Fail("Should have thrown a NotFoundException");
            }
            catch (NotFoundException)
            {

            }

            await task1;
        }

        [TestMethod]
        public async Task ValidateNotFoundOnBackgroundRefreshRemovesFromCacheScenario()
        {
            string value1 = "Response1Value";
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            string response1 = await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return value1;
                },
                false,
                null);

            Assert.AreEqual(value1, response1);

            string response2 = await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new Exception("Should use cached value");
                },
                false,
                null);

            Assert.AreEqual(value1, response2);

            NotFoundException notFoundException = new NotFoundException("Item was deleted");
            try
            {
                await asyncCache.GetAsync(
                    "test",
                    async (staleValue) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(5));
                        throw notFoundException;
                    },
                    true,
                    null);
                Assert.Fail("Should have thrown a NotFoundException");
            }
            catch (NotFoundException exception)
            {
                Assert.AreEqual(notFoundException, exception);
            }

            string valueAfterNotFound = "response4Value";
            string response4 = await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return valueAfterNotFound;
                },
                false,
                null);

            Assert.AreEqual(valueAfterNotFound, response4);
        }

        [TestMethod]
        public async Task ValidateAsyncCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            string result = await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return "test2";
                },
                false,
                (x, y) => throw new Exception("Should not be called since there is no refresh"));

            string cachedResults = await asyncCache.GetAsync(
                "test",
                async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new Exception("should not refresh");
                },
                false,
                (x, y) => throw new Exception("Should not be called since there is no refresh"));

            string oldValue = null;
            string newValue = null;
            Task<string> updateTask = asyncCache.GetAsync(
                key: "test",
                singleValueInitFunc: async (staleValue) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    return "Test3";
                },
                forceRefresh: true,
                callBackOnForceRefresh: (x, y) => {oldValue = x; newValue = y;});

            Stopwatch concurrentOperationStopwatch = Stopwatch.StartNew();
            string concurrentUpdateTask = await asyncCache.GetAsync(
                "test",
                 async (staleValue) =>
                 {
                     await Task.Delay(TimeSpan.FromMilliseconds(5));
                     throw new Exception("should not refresh");
                 },
                false,
                (x, y) => throw new Exception("Should not be called since there is no refresh"));
            Assert.AreEqual("test2", result);
            concurrentOperationStopwatch.Stop();

            Assert.IsTrue(concurrentOperationStopwatch.Elapsed.TotalMilliseconds < 500);

            result = await updateTask;
            Assert.AreEqual("Test3", result);
            Assert.AreEqual(oldValue, "test2", "The call back was not done.");
            Assert.AreEqual(newValue, "Test3", "The call back was not done.");
        }

        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateCacheValueIsRemovedAfterException()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            string result = await asyncCache.GetAsync(
                "test",
                (staleValue) => Task.FromResult("test2"),
                false,
                null);
            Assert.AreEqual("test2", result);

            string cachedResults = await asyncCache.GetAsync(
                "test",
                (staleValue) => throw new Exception("should not refresh"),
                false,
                null);
            Assert.AreEqual("test2", cachedResults);

            // Simulate a slow connection on a refresh operation. The async call will
            // be blocked to verify a read can still be done using the stale value.
            bool delayException = true;
            Task task = Task.Run(async () =>
            {
                try
                {
                    await asyncCache.GetAsync(
                        "test",
                        async (staleValue) =>
                        {
                            while (delayException)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(1));
                            }

                            throw new NotFoundException("testNotFoundException");
                        },
                        true,
                        null);
                    Assert.Fail();
                }
                catch (NotFoundException nfe)
                {
                    Assert.IsTrue(nfe.Message.Contains("testNotFoundException"), $"NotFoundException message is missing: {nfe.Message}");
                }
            });

            cachedResults = await asyncCache.GetAsync(
               "test",
               (staleValue) => throw new Exception("should not refresh"),
               false,
               null);
            Assert.AreEqual("test2", cachedResults);

            delayException = false;

            await task;
        }


        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateConcurrentCreateAsyncCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            int totalLazyCalls = 0;

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 500; i++)
            {
                tasks.Add(Task.Run(() => asyncCache.GetAsync(
                    "key",
                    (staleValue) =>
                    {
                        Interlocked.Increment(ref totalLazyCalls);
                        return Task.FromResult("Test");
                    },
                    forceRefresh: false,
                    (x, y) => throw new Exception("Should not be called since there is no refresh"))));
            }

            await Task.WhenAll(tasks);
            Assert.AreEqual(1, totalLazyCalls);
        }

        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateConcurrentCreateWithFailureAsyncCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            int totalLazyCalls = 0;

            Random random = new Random();
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                // Insert a random delay to simulate multiple request coming at different times
                await Task.Delay(random.Next(0, 5));
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await asyncCache.GetAsync(
                            "key",
                            async (staleValue) =>
                            {
                                Interlocked.Increment(ref totalLazyCalls);
                                await Task.Delay(random.Next(0, 3));
                                throw new NotFoundException("test");
                            },
                            forceRefresh: false,
                            (x, y) => throw new Exception("Should not be called since there is no refresh"));
                        Assert.Fail();
                    }
                    catch (DocumentClientException dce)
                    {
                        Assert.AreEqual(dce.StatusCode, HttpStatusCode.NotFound);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Assert.IsTrue(totalLazyCalls > 1, $"Expected multiple async refresh call. TotalCount {totalLazyCalls}");
        }

        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateExceptionScenariosCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>();
            int totalLazyCalls = 0;

            try
            {
                await asyncCache.GetAsync(
                    "key",
                    async (staleValue) =>
                    {
                        // Use a dummy await to make it simulate a real async network call
                        await Task.CompletedTask;
                        Interlocked.Increment(ref totalLazyCalls);
                        throw new DocumentClientException("test", HttpStatusCode.NotFound, SubStatusCodes.Unknown);
                    },
                    forceRefresh: false,
                    null);
                Assert.Fail();
            }
            catch (DocumentClientException dce)
            {
                Assert.AreEqual(dce.StatusCode, HttpStatusCode.NotFound);
                Assert.AreEqual(1, totalLazyCalls);
            }

            // Verify cache removes the Task that hit the exception and the new task
            // is used to try to get the value.
            totalLazyCalls = 0;
            try
            {
                await asyncCache.GetAsync(
                    "key",
                    async (staleValue) =>
                    {
                        // Use a dummy await to make it simulate a real async network call
                        await Task.CompletedTask;
                        Interlocked.Increment(ref totalLazyCalls);
                        throw new DocumentClientException("test", HttpStatusCode.BadRequest, SubStatusCodes.Unknown);
                    },
                    forceRefresh: false,
                    null);
                Assert.Fail();
            }
            catch (DocumentClientException dce)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, dce.StatusCode);
                Assert.AreEqual(1, totalLazyCalls);
            }

            // Verify cache success after failures
            totalLazyCalls = 0;
            string result = await asyncCache.GetAsync(
                    "key",
                    async (staleValue) =>
                    {
                        // Use a dummy await to make it simulate a real async network call
                        await Task.CompletedTask;
                        Interlocked.Increment(ref totalLazyCalls);
                        return "Test3";
                    },
                    forceRefresh: false,
                    null);
            Assert.AreEqual(1, totalLazyCalls);
            Assert.AreEqual("Test3", result);
        }
    }
}