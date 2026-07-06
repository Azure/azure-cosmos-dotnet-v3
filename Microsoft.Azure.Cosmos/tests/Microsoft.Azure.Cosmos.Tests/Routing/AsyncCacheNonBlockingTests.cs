//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;


    [TestClass]
    public class AsyncCacheNonBlockingTests
    {
        [TestMethod]
        [Owner("jawilley")]
        [DataRow(true)]
        [DataRow(false)]
        [ExpectedException(typeof(NotFoundException))]
        public async Task ValidateNegativeScenario(bool forceRefresh)
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            await asyncCache.GetAsync(
                "test",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new NotFoundException("testNotFoundException");
                },
                (_) => forceRefresh);
        }

        [TestMethod]
        public async Task ValidateMultipleBackgroundRefreshesScenario()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);

            string expectedValue = "ResponseValue";
            string response = await asyncCache.GetAsync(
                "test",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return expectedValue;
                },
                (_) => false);

            Assert.AreEqual(expectedValue, response);

            for (int i = 0; i < 10; i++)
            {
                string forceRefreshResponse = await asyncCache.GetAsync(
                    key: "test",
                    singleValueInitFunc: async (_) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(5));
                        return expectedValue + i;
                    },
                    forceRefresh: (_) => true);

                Assert.AreEqual(expectedValue + i, forceRefreshResponse);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(NotFoundException))]
        public async Task ValidateNegativeNotAwaitedScenario()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            Task task1 = asyncCache.GetAsync(
                "test",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new NotFoundException("testNotFoundException");
                },
                (_) => false);

            try
            {
                await asyncCache.GetAsync(
                    "test",
                    (_) => throw new BadRequestException("testBadRequestException"),
                    (_) => false);
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
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            string response1 = await asyncCache.GetAsync(
                "test",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return value1;
                },
                (staleValue) =>
                {
                    Assert.AreEqual(null, staleValue);
                    return false;
                });

            Assert.AreEqual(value1, response1);

            string response2 = await asyncCache.GetAsync(
                "test",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw new Exception("Should use cached value");
                },
                 (staleValue) =>
                 {
                     Assert.AreEqual(value1, staleValue);
                     return false;
                 });

            Assert.AreEqual(value1, response2);

            NotFoundException notFoundException = new NotFoundException("Item was deleted");
            try
            {
                await asyncCache.GetAsync(
                    "test",
                    async (_) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(5));
                        throw notFoundException;
                    },
                    (_) => true);
                Assert.Fail("Should have thrown a NotFoundException");
            }
            catch (NotFoundException exception)
            {
                Assert.AreEqual(notFoundException, exception);
            }

            string valueAfterNotFound = "response4Value";
            string response4 = await asyncCache.GetAsync(
                "test",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    return valueAfterNotFound;
                },
                (_) => false);

            Assert.AreEqual(valueAfterNotFound, response4);
        }

        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateAsyncCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            string result = await asyncCache.GetAsync(
                "test",
                (_) => Task.FromResult("test2"),
                (_) => false);

            string cachedResults = await asyncCache.GetAsync(
                "test",
                (_) => throw new Exception("should not refresh"),
                (_) => false);

            string oldValue = null;
            Task<string> updateTask = asyncCache.GetAsync(
                key: "test",
                singleValueInitFunc: async (staleValue) =>
                {
                    oldValue = staleValue;
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    return "Test3";
                },
                forceRefresh: (_) => true);

            ValueStopwatch concurrentOperationStopwatch = ValueStopwatch.StartNew();
            string concurrentUpdateTask = await asyncCache.GetAsync(
                "test",
                (_) => throw new Exception("should not refresh"),
                (_) => false);
            Assert.AreEqual("test2", result);
            concurrentOperationStopwatch.Stop();

            Assert.IsTrue(concurrentOperationStopwatch.Elapsed.TotalMilliseconds < 500);

            result = await updateTask;
            Assert.AreEqual("Test3", result);
            Assert.AreEqual(oldValue, "test2", "The call back was not done.");
        }

        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateCacheValueIsRemovedAfterException()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            string result = await asyncCache.GetAsync(
                key: "test",
                singleValueInitFunc: (_) => Task.FromResult("test2"),
                forceRefresh: (_) => false);
            Assert.AreEqual("test2", result);

            string cachedResults = await asyncCache.GetAsync(
                key: "test",
                singleValueInitFunc: (_) => throw new Exception("should not refresh"),
                forceRefresh: (_) => false);
            Assert.AreEqual("test2", cachedResults);

            // Simulate a slow connection on a refresh operation. The async call will
            // be blocked to verify a read can still be done using the stale value.
            bool delayException = true;
            Task task = Task.Run(async () =>
            {
                try
                {
                    await asyncCache.GetAsync(
                        key: "test",
                        singleValueInitFunc: async (_) =>
                        {
                            while (delayException)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(1));
                            }

                            throw new NotFoundException("testNotFoundException");
                        },
                        forceRefresh: (_) => true);
                    Assert.Fail();
                }
                catch (NotFoundException nfe)
                {
                    Assert.IsTrue(nfe.Message.Contains("testNotFoundException"), $"NotFoundException message is missing: {nfe.Message}");
                }
            });

            cachedResults = await asyncCache.GetAsync(
               key: "test",
               singleValueInitFunc: (_) => throw new Exception("should not refresh"),
               forceRefresh: (_) => false);
            Assert.AreEqual("test2", cachedResults);

            delayException = false;

            await task;
        }


        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateConcurrentCreateAsyncCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            int totalLazyCalls = 0;

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 500; i++)
            {
                tasks.Add(Task.Run(() => asyncCache.GetAsync(
                    key: "key",
                    singleValueInitFunc: (_) =>
                    {
                        Interlocked.Increment(ref totalLazyCalls);
                        return Task.FromResult("Test");
                    },
                    forceRefresh: (_) => false)));
            }

            await Task.WhenAll(tasks);
            Assert.AreEqual(1, totalLazyCalls);
        }

        [TestMethod]
        [Owner("jawilley")]
        public async Task ValidateConcurrentCreateWithFailureAsyncCacheNonBlocking()
        {
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
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
                            key: "key",
                            singleValueInitFunc: async (_) =>
                            {
                                Interlocked.Increment(ref totalLazyCalls);
                                await Task.Delay(random.Next(0, 3));
                                throw new NotFoundException("test");
                            },
                            forceRefresh: (_) => false);
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
            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);
            int totalLazyCalls = 0;

            try
            {
                await asyncCache.GetAsync(
                    key: "key",
                    singleValueInitFunc: async (_) =>
                    {
                        // Use a dummy await to make it simulate a real async network call
                        await Task.CompletedTask;
                        Interlocked.Increment(ref totalLazyCalls);
                        throw new DocumentClientException("test", HttpStatusCode.NotFound, SubStatusCodes.Unknown);
                    },
                    forceRefresh: (_) => false);
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
                    key: "key",
                    singleValueInitFunc: async (_) =>
                    {
                        // Use a dummy await to make it simulate a real async network call
                        await Task.CompletedTask;
                        Interlocked.Increment(ref totalLazyCalls);
                        throw new DocumentClientException("test", HttpStatusCode.BadRequest, SubStatusCodes.Unknown);
                    },
                    forceRefresh: (_) => false);
                Assert.Fail();
            }
            catch (DocumentClientException dce)
            {
                Assert.AreEqual(dce.StatusCode, HttpStatusCode.BadRequest);
                Assert.AreEqual(1, totalLazyCalls);
            }

            // Verify cache success after failures
            totalLazyCalls = 0;
            string result = await asyncCache.GetAsync(
                    key: "key",
                    singleValueInitFunc: async (_) =>
                    {
                        // Use a dummy await to make it simulate a real async network call
                        await Task.CompletedTask;
                        Interlocked.Increment(ref totalLazyCalls);
                        return "Test3";
                    },
                    forceRefresh: (_) => false);
            Assert.AreEqual(1, totalLazyCalls);
            Assert.AreEqual("Test3", result);
        }

        /// <summary>
        /// Test to validate that when Refresh() is invoked for a valid existing key, the
        /// cache refreshes the key successfully and the new value is updated in the cache.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task Refresh_WhenRefreshRequestedForAnExistingKey_ShouldRefreshTheCache()
        {
            // Arrange.
            AsyncCacheNonBlocking<string, string> asyncCache = new (enableAsyncCacheExceptionNoSharing: false);

            // Act and Assert.
            string result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value1"),
                (_) => false);

            Assert.AreEqual("value1", result);

            asyncCache.Refresh(
                "key",
                (_) => Task.FromResult("value2"));

            // Add some delay for the background refresh task to complete.
            await Task.Delay(
                millisecondsDelay: 50);

            result = await asyncCache.GetAsync(
                "key",
                (_) => throw new Exception("Should not refresh."),
                (_) => false);

            Assert.AreEqual("value2", result);
        }

        /// <summary>
        /// Test to validate that when a DocumentClientException is thrown during Refresh() operation,
        /// then the cache removes the key for which a refresh was requested.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task Refresh_WhenThrowsDocumentClientException_ShouldRemoveKeyFromTheCache()
        {
            // Arrange.
            AsyncCacheNonBlocking<string, string> asyncCache = new (enableAsyncCacheExceptionNoSharing: false);

            // Act and Assert.
            string result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value1"),
                (_) => false);

            Assert.AreEqual("value1", result);

            result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value2"),
                (_) => false);

            // Because the key is already present in the cache and a force refresh was not requested
            // the func delegate should not get invoked and thus the cache should not be updated
            // and still return the old cached value.
            Assert.AreEqual("value1", result);

            NotFoundException notFoundException = new(
                message: "Item was deleted.");

            asyncCache.Refresh(
                "key",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw notFoundException;
                });

            // Add some delay for the background refresh task to complete.
            await Task.Delay(
                millisecondsDelay: 50);

            // Because the key was deleted from the cache, the func delegate should get invoked at
            // this point and update the value to value2.
            result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value2"),
                (_) => false);

            Assert.AreEqual("value2", result);
        }

        /// <summary>
        /// Test to validate that when some other Exception is thrown during Refresh() operation,
        /// then the cache does not remove the key for which the refresh was originally requested.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task Refresh_WhenThrowsOtherException_ShouldNotRemoveKeyFromTheCache()
        {
            // Arrange.
            AsyncCacheNonBlocking<string, string> asyncCache = new (enableAsyncCacheExceptionNoSharing: false);

            // Act and Assert.
            string result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value1"),
                (_) => false);

            Assert.AreEqual("value1", result);

            result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value2"),
                (_) => false);

            // Because the key is already present in the cache and a force refresh was not requested
            // the func delegate should not get invoked and thus the cache should not be updated
            // and still return the old cached value.
            Assert.AreEqual("value1", result);

            Exception exception = new(
                message: "Timeout exception.");

            asyncCache.Refresh(
                "key",
                async (_) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                    throw exception;
                });

            // Because the key should not get deleted from the cache, the func delegate should not get invoked at
            // this point.
            result = await asyncCache.GetAsync(
                "key",
                (_) => Task.FromResult("value2"),
                (_) => false);

            Assert.AreEqual("value1", result);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ValidateCacheGetAsyncExceptionPostProcessing(bool enabled)
        {
            // Arrange
            Exception testException = new TimeoutException("Simulated timeout exception");
            CancellationToken cancellationToken = CancellationToken.None;

            AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: enabled);

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
                            key: "testKey",
                            singleValueInitFunc: async (_) =>
                            {
                                await Task.Delay(5);
                                throw testException;
                            },
                            forceRefresh: (_) => false);
                        Assert.Fail();
                    }
                    catch (TimeoutException dce)
                    {
                        if (enabled)
                        {
                            Assert.IsFalse(Object.ReferenceEquals(dce, testException));
                        }
                        else
                        {
                            Assert.IsTrue(Object.ReferenceEquals(dce, testException));
                        }
                    }
                }));
            }
        }

        [TestMethod]
        public async Task GetAsync_FailureLogging_DoesNotSerializeCosmosExceptionDiagnosticsWhenTracingDisabled()
        {
            // Call-site regression for #5945: the failure-path DefaultTrace.TraceError is gated behind
            // DiagnosticsHandlerHelper.ShouldTrace(...) (which wraps DefaultTrace.TraceSource.Switch.ShouldTrace),
            // so when tracing is disabled (the default
            // no-op sink) the heavyweight ex.Message -> Diagnostics serialization is never evaluated. The
            // spy's Diagnostics getter throws, and Message evaluates Diagnostics as an argument before
            // building the string, so an UN-gated call site (passing ex.Message without the ShouldTrace
            // guard) would surface here as DiagnosticsAccessed == true. A 503 is used because that is a
            // status code where Message eagerly serializes diagnostics in prod.
            SourceLevels originalLevel = DefaultTrace.TraceSource.Switch.Level;
            try
            {
                // Force the no-op sink scenario deterministically, independent of other tests in the suite.
                DefaultTrace.TraceSource.Switch.Level = SourceLevels.Off;

                // Precondition (separate instance so we don't poison the thrown instance's lazy Message
                // cache): a 503 CosmosException really does serialize diagnostics via Message, so the
                // DiagnosticsAccessed assertion below is meaningful.
                DiagnosticsThrowingCosmosException precondition = new DiagnosticsThrowingCosmosException(
                    HttpStatusCode.ServiceUnavailable,
                    "boom",
                    new Headers { ActivityId = "activity-5945" });
                Assert.ThrowsException<InvalidOperationException>(() => _ = precondition.Message);
                Assert.IsTrue(precondition.DiagnosticsAccessed, "Precondition: Message must serialize diagnostics for 503.");

                AsyncCacheNonBlocking<string, string> asyncCache = new AsyncCacheNonBlocking<string, string>(enableAsyncCacheExceptionNoSharing: false);

                DiagnosticsThrowingCosmosException toThrow = new DiagnosticsThrowingCosmosException(
                    HttpStatusCode.ServiceUnavailable,
                    "boom",
                    new Headers { ActivityId = "activity-5945" });

                DiagnosticsThrowingCosmosException caught = await Assert.ThrowsExceptionAsync<DiagnosticsThrowingCosmosException>(
                    () => asyncCache.GetAsync(
                        "test",
                        async (_) =>
                        {
                            await Task.Yield();
                            throw toThrow;
                        },
                        (_) => false));

                Assert.AreSame(toThrow, caught, "The original exception must propagate unchanged.");
                Assert.IsFalse(
                    toThrow.DiagnosticsAccessed,
                    "Failure-path logging must not serialize CosmosException diagnostics when tracing is disabled (the DefaultTrace call must be gated by DiagnosticsHandlerHelper.ShouldTrace).");
            }
            finally
            {
                DefaultTrace.TraceSource.Switch.Level = originalLevel;
            }
        }

        private sealed class DiagnosticsThrowingCosmosException : CosmosException
        {
            public DiagnosticsThrowingCosmosException(
                HttpStatusCode statusCode,
                string message,
                Headers headers)
                : base(statusCode, message, null, headers, Microsoft.Azure.Cosmos.Tracing.NoOpTrace.Singleton, null, null)
            {
            }

            public bool DiagnosticsAccessed { get; private set; }

            public override CosmosDiagnostics Diagnostics
            {
                get
                {
                    this.DiagnosticsAccessed = true;
                    throw new InvalidOperationException("Diagnostics must not be accessed by trace-safe logging (#5945).");
                }
            }
        }
    }
}
