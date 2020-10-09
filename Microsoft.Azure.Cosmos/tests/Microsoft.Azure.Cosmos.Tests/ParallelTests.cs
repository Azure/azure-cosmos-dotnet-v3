//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ParallelTests
    {
        [TestMethod]
        public async Task SimpleTest()
        {
            ThreadSafeCounter counter = new ThreadSafeCounter();
            int count = 100;
            await AsyncParallel.ForEachAllOrNothingAsync(
                Enumerable.Repeat<int>(element: 42, count: count),
                (source, cancellationToken) => counter.AddOneAsync(cancellationToken),
                maxDegreeOfParallelism: 1,
                cancellationToken: default);
            Assert.AreEqual(count, counter.Counter);
        }

        [TestMethod]
        public async Task SimpleTestWithParallelism()
        {
            ThreadSafeCounter counter = new ThreadSafeCounter();
            int count = 100;
            await AsyncParallel.ForEachAllOrNothingAsync(
                Enumerable.Repeat<int>(element: 42, count: count),
                (source, cancellationToken) => counter.AddOneAsync(cancellationToken),
                maxDegreeOfParallelism: 10,
                cancellationToken: default);
            Assert.AreEqual(count, counter.Counter);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task OneChildFailure()
        {
            ThreadSafeCounter counter = new ThreadSafeCounter();
            int problemChild = 42;
            await AsyncParallel.ForEachAllOrNothingAsync(
                Enumerable.Range(start: 0, count: 100),
                (source, cancellationToken) =>
                {
                    if (source == problemChild)
                    {
                        throw new Exception();
                    }

                    return counter.AddOneAsync(cancellationToken);
                },
                maxDegreeOfParallelism: 1,
                cancellationToken: default);
            Assert.AreEqual(problemChild, counter.Counter);
        }

        private sealed class ThreadSafeCounter
        {
            private readonly SemaphoreSlim mutex;
            private readonly TimeSpan? delay;

            public ThreadSafeCounter(TimeSpan? delay = null)
            {
                this.mutex = new SemaphoreSlim(initialCount: 1, maxCount: 1);
                this.delay = delay;
            }

            public int Counter { get; private set; }

            public async Task AddOneAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (this.delay.HasValue)
                {
                    await Task.Delay(this.delay.Value);
                }

                await this.mutex.WaitAsync();
                this.Counter++;
                this.mutex.Release();
            }
        }
    }
}
