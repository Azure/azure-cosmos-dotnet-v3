//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AsyncCacheTest
    {
        [TestMethod]
        [Owner("flnarenj")]
        public async Task TestConcurrentGetAsync()
        {
            const string key = "a";
            const string value = "b";

            AsyncCache<string, string> cache = new AsyncCache<string, string>();

            int hitCount = 0;
            await Enumerable.Range(0, 10).ForEachAsync(
               10,
               _ => cache.GetAsync(
                   key,
                   null,
                   () =>
                   {
                       // No Interlocked, would force barriers
                       hitCount++;
                       return Task.FromResult(value);
                   },
                   CancellationToken.None));

            Assert.AreEqual(1, hitCount);
        }

        [TestMethod]
        public async Task TestGetAsync()
        {
            int numberOfCacheRefreshes = 0;
            Func<int, CancellationToken, Task<int>> refreshFunc = (key, cancellationToken) =>
                {
                    Interlocked.Increment(ref numberOfCacheRefreshes);
                    return Task.FromResult(key * 2);
                };

            AsyncCache<int, int> cache = new AsyncCache<int, int>();

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    int key = j;
                    tasks.Add(cache.GetAsync(key, -1, () => refreshFunc(key, CancellationToken.None), CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks);

            Assert.AreEqual(10, numberOfCacheRefreshes);

            Assert.AreEqual(4, await cache.GetAsync(2, -1, () => refreshFunc(2, CancellationToken.None), CancellationToken.None));

            Func<int, CancellationToken, Task<int>> refreshFunc1 = (key, cancellationToken) =>
            {
                Interlocked.Increment(ref numberOfCacheRefreshes);
                return Task.FromResult((key * 2) + 1);
            };

            List<Task> tasks1 = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    int key = j;
                    tasks1.Add(cache.GetAsync(key, key * 2, () => refreshFunc1(key, CancellationToken.None), CancellationToken.None));
                }

                for (int j = 0; j < 10; j++)
                {
                    int key = j;
                    tasks1.Add(cache.GetAsync(key, key * 2, () => refreshFunc1(key, CancellationToken.None), CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks1);

            Assert.AreEqual(20, numberOfCacheRefreshes);
            Assert.AreEqual(5, await cache.GetAsync(2, -1, () => refreshFunc(2, CancellationToken.None), CancellationToken.None));
        }

        /// <summary>
        /// The scenario tested here is that when a generator throws an
        /// exception it removes itself from the cache. This behavior
        /// is to prevent indefinite caching of items which are never used.
        /// </summary>
        [TestMethod]
        public async Task TestRemoveWhenGeneratorThrowsException()
        {
            AsyncCache<int, int> cache = new AsyncCache<int, int>();
            using SemaphoreSlim resetEventSlim = new SemaphoreSlim(0, 1);

            Func<Task<int>> generatorFunc = () => Task.Run(async () =>
            {
                await resetEventSlim.WaitAsync();
                throw new Exception(nameof(TestRemoveWhenGeneratorThrowsException));
#pragma warning disable CS0162 // Unreachable code detected
                return 1;
#pragma warning restore CS0162 // Unreachable code detected
            });

            Task<int> getTask = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc, cancellationToken: default);
            resetEventSlim.Release();
            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => getTask);
            Assert.AreEqual(nameof(TestRemoveWhenGeneratorThrowsException), ex.Message);
            Assert.IsFalse(cache.Keys.Contains(1));
        }

        /// <summary>
        /// The scenario tested here is a concurrent race when
        /// Thread 1 enqueues a task and is currently awaiting its completion
        /// Before the task is completed, Thread 2 requests the same key
        /// Because it's concurrent it'll get back the task from Thread1.
        /// If thread 1 then cancels (passed in cancellation token gets canceled)
        /// then Thread 2 will also fail with the same exception.
        /// </summary>
        [TestMethod]
        public async Task TestCancelOnOneThreadDoesNotCancelAnother()
        {
            AsyncCache<int, int> cache = new AsyncCache<int, int>();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            SemaphoreSlim resetEventSlim = new SemaphoreSlim(0, 1);

            Func<Task<int>> generatorFunc1 = () => Task.Run(async () =>
            {
                await resetEventSlim.WaitAsync(cancellationTokenSource.Token);
                return 1;
            }, cancellationTokenSource.Token);

            Func<Task<int>> generatorFunc2 = () => Task.Run(() => 2);

            // set up two threads that are concurrently updating the async cache for the same key.
            // the only difference is that one thread passes in a cancellation token
            // and the other does not.
            Task<int> getTask1 = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc1, cancellationToken: cancellationTokenSource.Token);

            Task<int> getTask2 = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc2, cancellationToken: CancellationToken.None);

            // assert that the tasks haven't completed.
            Assert.IsFalse(getTask2.IsCompleted);
            Assert.IsFalse(getTask1.IsCompleted);

            // cancel the first task's cancellation token.
            cancellationTokenSource.Cancel();

            // neither task is complete at this point.
            Assert.IsFalse(getTask2.IsCompleted);
            Assert.IsFalse(getTask1.IsCompleted);

            try
            {
                await getTask1;
                Assert.Fail("Should fail because of cancellation.");
            }
            catch (TaskCanceledException)
            {
            }

            // task 2 should not fail because task 1 got cancelled.
            int getTaskResult2 = await getTask2;
            Assert.AreEqual(2, getTaskResult2);
        }

        [TestMethod]
        public async Task TestCancelOnOneThreadCancelsOtherTaskIfCanceled()
        {
            AsyncCache<int, int> cache = new AsyncCache<int, int>();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            SemaphoreSlim resetEventSlim = new SemaphoreSlim(0, 1);

            bool generatorFunc2Called = false;
            Func<Task<int>> generatorFunc1 = () => Task.Run(async () =>
            {
                await resetEventSlim.WaitAsync(cancellationTokenSource.Token);
                return 1;
            }, cancellationTokenSource.Token);

            Func<Task<int>> generatorFunc2 = () => Task.Run(() =>
            {
                generatorFunc2Called = true;
                return 2;
            });

            // set up two threads that are concurrently updating the async cache for the same key.
            // the only difference is that one thread passes in a cancellation token
            // and the other does not.
            Task<int> getTask1 = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc1, cancellationToken: cancellationTokenSource.Token);

            Task<int> getTask2 = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc2, cancellationToken: cancellationTokenSource.Token);

            // assert that the tasks haven't completed.
            Assert.IsFalse(getTask2.IsCompleted);
            Assert.IsFalse(getTask1.IsCompleted);

            // cancel the first task's cancellation token.
            cancellationTokenSource.Cancel();

            // neither task is complete at this point.
            Assert.IsFalse(getTask2.IsCompleted);

            try
            {
                await getTask1;
                Assert.Fail("Should fail because of cancellation.");
            }
            catch (Exception e)
            {
                Assert.IsTrue(e as OperationCanceledException != null, e.Message);
            }

            try
            {
                await getTask2;
                Assert.Fail("Should fail because of cancellation.");
            }
            catch (Exception e)
            {
                Assert.IsTrue(e as OperationCanceledException != null, e.Message);
            }

            Assert.IsFalse(generatorFunc2Called);
        }

        [TestMethod]
        public async Task TestFailureOnOneThreadDoesNotFailAnother()
        {
            AsyncCache<int, int> cache = new AsyncCache<int, int>();
            SemaphoreSlim resetEventSlim = new SemaphoreSlim(0, 1);

            Func<Task<int>> generatorFunc1 = () => Task<int>.Run(async () =>
            {
                await resetEventSlim.WaitAsync();
                return this.GenerateIntFuncThatThrows();
            });

            Func<Task<int>> generatorFunc2 = () => Task.Run(() => 2);

            // set up two threads that are concurrently updating the async cache for the same key.
            // the only difference is that one thread passes in a cancellation token
            // and the other does not.
            Task<int> getTask1 = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc1, cancellationToken: CancellationToken.None);

            Task<int> getTask2 = cache.GetAsync(key: 1, obsoleteValue: -1, singleValueInitFunc: generatorFunc2, cancellationToken: CancellationToken.None);

            // assert that the tasks haven't completed.
            Assert.IsFalse(getTask2.IsCompleted);
            Assert.IsFalse(getTask1.IsCompleted);

            // release a thread that causes the first to throw.
            resetEventSlim.Release();

            // neither task is complete at this point.
            Assert.IsFalse(getTask2.IsCompleted);
            Assert.IsFalse(getTask1.IsCompleted);

            try
            {
                await getTask1;
                Assert.Fail("Should fail because of exception.");
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(InvalidOperationException), e.GetType(), e.Message);
            }

            // task 2 should not fail because task 1 got cancelled.
            int getTaskResult2 = await getTask2;
            Assert.AreEqual(2, getTaskResult2);
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task TestAsyncDeadlock()
        {
            AsyncCache<int, int> cache = new AsyncCache<int, int>();
            ValueStopwatch stopwatch = new ValueStopwatch();

            stopwatch.Start();
            await Task.Factory.StartNew(() =>
            {
                stopwatch.Stop();
                Logger.LogLine($"TestAsyncDeadlock Factory started in {stopwatch.ElapsedMilliseconds} ms");
                cache.Set(0, 42);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            new SingleTaskScheduler()
            );
        }

        private int GenerateIntFuncThatThrows()
        {
            throw new InvalidOperationException();
        }
    }
}