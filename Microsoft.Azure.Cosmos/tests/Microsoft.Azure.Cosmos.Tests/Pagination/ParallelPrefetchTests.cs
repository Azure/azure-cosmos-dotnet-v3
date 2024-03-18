namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Buffers;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ParallelPrefetchTests
    {
        /// <summary>
        /// IPrefetcher which can only be run once, and invokes callbacks as it executes.
        /// </summary>
        private sealed class TestOncePrefetcher : IPrefetcher
        {
            private int hasRun;

            private readonly Action beforeAwait;
            private readonly Action afterAwait;

            internal TestOncePrefetcher(Action beforeAwait, Action afterAwait)
            {
                this.hasRun = 0;
                this.beforeAwait = beforeAwait;
                this.afterAwait = afterAwait;
            }

            public async ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
            {
                // test that ParallelPrefetch doesn't start the same Task twice.
                int oldRun = Interlocked.Exchange(ref this.hasRun, 1);
                Assert.AreEqual(0, oldRun);

                // we use two callbacks to test that ParallelPrefetch is correctly monitoring
                // continuations - without this, we might incorrectly consider a Task completed
                // despite it awaiting an inner Task

                this.beforeAwait();

                await Task.Yield();

                this.afterAwait();
            }
        }

        /// <summary>
        /// IEnumerable which throws if touched.
        /// </summary>
        private sealed class ThrowsEnumerable<T> : IEnumerable<T>
        {
            public IEnumerator<T> GetEnumerator()
            {
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        /// <summary>
        /// IPrefetcher which throws if touched.
        /// </summary>
        private sealed class ThrowsPrefetcher : IPrefetcher
        {
            public ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// ArrayPool that tracks leaks, double returns, and includes non-null values in
        /// returned arrays.
        /// </summary>
        private sealed class ValidatingRandomizedArrayPool<T> : ArrayPool<T>
            where T : class
        {
            private readonly T existingValue;

            private readonly ConcurrentBag<T[]> created;
            private readonly ConcurrentDictionary<T[], object> rented;

            internal ValidatingRandomizedArrayPool(T existingValue)
            {
                this.existingValue = existingValue;
                this.created = new();
                this.rented = new();
            }

            public override T[] Rent(int minimumLength)
            {
                int extra = Random.Shared.Next(6);

                T[] ret = new T[minimumLength + extra];
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = Random.Shared.Next(2) == 0 ? this.existingValue : null;
                }

                this.created.Add(ret);

                Assert.IsTrue(this.rented.TryAdd(ret, null));

                return ret;
            }

            public override void Return(T[] array, bool clearArray = false)
            {
                Assert.IsFalse(clearArray, "Caller should clean up array itself");

                Assert.IsTrue(this.rented.TryRemove(array, out _), "Tried to return array that isn't rented");

                for (int i = 0; i < array.Length; i++)
                {
                    object value = array[i];

                    if (object.ReferenceEquals(value, this.existingValue))
                    {
                        continue;
                    }

                    Assert.IsNull(value, "Returned array shouldn't have any non-null values, except those included by the original Rent call");
                }
            }

            internal void AssertAllReturned()
            {
                Assert.IsTrue(this.rented.IsEmpty);
            }
        }

        /// <summary>
        /// ITrace which does nothing.
        /// </summary>
        private sealed class NullTrace : ITrace
        {
            public string Name => "Null";

            public Guid Id => Guid.Empty;

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.MaxValue;

            public TraceLevel Level => TraceLevel.Off;

            public TraceComponent Component => TraceComponent.Unknown;

            public TraceSummary Summary => new TraceSummary();

            public ITrace Parent => null;

            public IReadOnlyList<ITrace> Children => new List<ITrace>();

            public IReadOnlyDictionary<string, object> Data => new Dictionary<string, object>();

            public void AddChild(ITrace trace)
            {
            }

            public void AddDatum(string key, TraceDatum traceDatum)
            {
            }

            public void AddDatum(string key, object value)
            {
            }

            public void AddOrUpdateDatum(string key, object value)
            {
            }

            public void Dispose()
            {
            }

            public ITrace StartChild(string name)
            {
                return this;
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level)
            {
                return this;
            }
        }

        /// <summary>
        /// Different task counts which explore different code paths.
        /// </summary>
        private static readonly int[] TaskCounts = new[] { 1, 2, 511, 512, 513, 1024, 1025 };

        /// <summary>
        /// Different max concurrencies which explore different code paths.
        /// </summary>
        private static readonly int[] Concurrencies = new[] { 1, 2, 511, 512, 513, int.MaxValue };

        private static readonly ITrace EmptyTrace = new NullTrace();

        [TestMethod]
        public async Task ZeroConcurrencyOptimizationAsync()
        {
            // test that we correctly special case maxConcurrency == 0 as "do nothing"

            IEnumerable<IPrefetcher> prefetchers = new ThrowsEnumerable<IPrefetcher>();

            await ParallelPrefetch.PrefetchInParallelAsync(
                prefetchers,
                0,
                EmptyTrace,
                default);
        }

        [TestMethod]
        public async Task AllExecutedAsync()
        {
            // test that all prefetchers are actually invoked

            foreach (int maxConcurrency in Concurrencies)
            {
                foreach (int taskCount in TaskCounts)
                {
                    int executed1 = 0;
                    int executed2 = 0;
                    IEnumerable<IPrefetcher> prefetchers = CreatePrefetchers(taskCount, () => Interlocked.Increment(ref executed1), () => Interlocked.Increment(ref executed2));

                    await ParallelPrefetch.PrefetchInParallelAsync(
                        prefetchers,
                        maxConcurrency,
                        EmptyTrace,
                        default);

                    Assert.AreEqual(taskCount, executed1);
                    Assert.AreEqual(taskCount, executed2);
                }
            }

            static IEnumerable<IPrefetcher> CreatePrefetchers(int count, Action beforeAwait, Action afterAwait)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new TestOncePrefetcher(beforeAwait, afterAwait);
                }
            }
        }

        [TestMethod]
        public async Task MaxConcurrencyRespectedAsync()
        {
            // test that we never get above maxConcurrency
            //
            // whether or not we _reach_ it is dependent on the scheduler
            // so we can't reliably test that

            foreach (int maxConcurrency in Concurrencies)
            {
                foreach (int taskCount in TaskCounts)
                {
                    int observedMax = 0;
                    int current = 0;

                    IEnumerable<IPrefetcher> prefetchers =
                        CreatePrefetchers(
                            taskCount,
                            () =>
                            {
                                int newCurrent = Interlocked.Increment(ref current);
                                Assert.IsTrue(newCurrent <= maxConcurrency);

                                int oldMax = Volatile.Read(ref observedMax);

                                while (newCurrent > oldMax)
                                {
                                    oldMax = Interlocked.CompareExchange(ref observedMax, newCurrent, oldMax);
                                }
                            },
                            () =>
                            {
                                int newCurrent = Interlocked.Decrement(ref current);

                                Assert.IsTrue(current >= 0);
                            });

                    await ParallelPrefetch.PrefetchInParallelAsync(
                        prefetchers,
                        maxConcurrency,
                        EmptyTrace,
                        default);

                    Assert.IsTrue(Volatile.Read(ref observedMax) <= maxConcurrency);
                    Assert.AreEqual(0, Volatile.Read(ref current));
                }
            }

            static IEnumerable<IPrefetcher> CreatePrefetchers(int count, Action beforeAwait, Action afterAwait)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new TestOncePrefetcher(beforeAwait, afterAwait);
                }
            }
        }

        [TestMethod]
        public async Task RentedBuffersAllReturnedAsync()
        {
            Task faultedTask = Task.FromException(new NotSupportedException());

            try
            {
                foreach (int maxConcurrency in Concurrencies)
                {
                    foreach (int taskCount in TaskCounts)
                    {
                        IEnumerable<IPrefetcher> prefetchers = CreatePrefetchers(taskCount, static () => { }, static () => { });

                        ValidatingRandomizedArrayPool<IPrefetcher> prefetcherPool = new ValidatingRandomizedArrayPool<IPrefetcher>(new ThrowsPrefetcher());
                        ValidatingRandomizedArrayPool<Task> taskPool = new ValidatingRandomizedArrayPool<Task>(faultedTask);
                        ValidatingRandomizedArrayPool<object> objectPool = new ValidatingRandomizedArrayPool<object>(new object());

                        ParallelPrefetch.ParallelPrefetchTestConfig config =
                            new ParallelPrefetch.ParallelPrefetchTestConfig(
                                prefetcherPool,
                                taskPool,
                                objectPool
                            );

                        await ParallelPrefetch.PrefetchInParallelImplAsync(
                            prefetchers,
                            maxConcurrency,
                            EmptyTrace,
                            config,
                            default);

                        prefetcherPool.AssertAllReturned();
                        taskPool.AssertAllReturned();
                        objectPool.AssertAllReturned();
                    }
                }
            }
            finally
            {
                // observe this intentionally faulted task, no matter what
                try
                {
                    await faultedTask;
                }
                catch
                {
                    // intentionally empty
                }
            }

            static IEnumerable<IPrefetcher> CreatePrefetchers(int count, Action beforeAwait, Action afterAwait)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new TestOncePrefetcher(beforeAwait, afterAwait);
                }
            }
        }
    }
}
