namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Buffers;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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

                cancellationToken.ThrowIfCancellationRequested();

                // we use two callbacks to test that ParallelPrefetch is correctly monitoring
                // continuations - without this, we might incorrectly consider a Task completed
                // despite it awaiting an inner Task

                this.beforeAwait();
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Yield();

                this.afterAwait();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// IPrefetcher that does complicated things.
        /// </summary>
        private sealed class ComplicatedPrefetcher : IPrefetcher
        {
            public long StartTimestamp { get; private set; }
            public long AfterYieldTimestamp { get; private set; }
            public long AfterDelay1Timestamp { get; private set; }
            public long AfterSemaphoreTimestamp { get; private set; }
            public long AfterDelay2Timestamp { get; private set; }
            public long AfterDelay3Timestamp { get; private set; }
            public long AfterDelay4Timestamp { get; private set; }
            public long WhenAllTimestamp { get; private set; }
            public long EndTimestamp { get; private set; }

            public async ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
            {
                this.StartTimestamp = Stopwatch.GetTimestamp();

                await Task.Yield();

                this.AfterYieldTimestamp = Stopwatch.GetTimestamp();

                using (SemaphoreSlim semaphore = new SemaphoreSlim(0, 1))
                {
                    Task delay = Task.Delay(5).ContinueWith(_ => { this.AfterDelay1Timestamp = Stopwatch.GetTimestamp(); semaphore.Release(); });

                    await semaphore.WaitAsync();
                    this.AfterSemaphoreTimestamp = Stopwatch.GetTimestamp();

                    await delay;
                }

                await Task.WhenAll(
                    Task.Delay(2).ContinueWith(_ => this.AfterDelay2Timestamp = Stopwatch.GetTimestamp()),
                    Task.Delay(3).ContinueWith(_ => this.AfterDelay3Timestamp = Stopwatch.GetTimestamp()),
                    Task.Delay(4).ContinueWith(_ => this.AfterDelay4Timestamp = Stopwatch.GetTimestamp()));
                this.WhenAllTimestamp = Stopwatch.GetTimestamp();

                await Task.Yield();

                this.EndTimestamp = Stopwatch.GetTimestamp();
            }

            internal void AssertCorrect()
            {
                Assert.IsTrue(this.StartTimestamp > 0);

                Assert.IsTrue(this.AfterYieldTimestamp > this.StartTimestamp);
                Assert.IsTrue(this.AfterDelay1Timestamp > this.AfterYieldTimestamp);
                Assert.IsTrue(this.AfterSemaphoreTimestamp > this.AfterDelay1Timestamp);

                // these can all fire in any order (delay doesn't guarantee any particular order)
                Assert.IsTrue(this.AfterDelay2Timestamp > this.AfterSemaphoreTimestamp);
                Assert.IsTrue(this.AfterDelay3Timestamp > this.AfterSemaphoreTimestamp);
                Assert.IsTrue(this.AfterDelay4Timestamp > this.AfterSemaphoreTimestamp);

                // but by WhenAll()'ing them, we can assert WhenAll completes after all the other delays
                Assert.IsTrue(this.WhenAllTimestamp > this.AfterDelay2Timestamp);
                Assert.IsTrue(this.WhenAllTimestamp > this.AfterDelay3Timestamp);
                Assert.IsTrue(this.WhenAllTimestamp > this.AfterDelay4Timestamp);

                Assert.IsTrue(this.EndTimestamp > this.WhenAllTimestamp);
            }
        }

        /// <summary>
        /// IPrefetcher that asserts it got a trace with an expected parent.
        /// </summary>
        private sealed class ExpectedParentTracePrefetcher : IPrefetcher
        {
            private readonly ITrace expectedParentTrace;

            internal ExpectedParentTracePrefetcher(ITrace expectedParentTrace)
            {
                this.expectedParentTrace = expectedParentTrace;
            }

            public ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
            {
                Assert.AreSame(this.expectedParentTrace, trace.Parent);

                return default;
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
        /// IEnumerable whose IEnumerator throws after a certain number of
        /// calls to MoveNext().
        /// </summary>
        private sealed class ThrowsAfterEnumerable<T> : IEnumerable<T>
        {
            private sealed class Enumerator : IEnumerator<T>
            {
                private readonly IEnumerator<T> inner;
                private readonly int throwAfter;
                private int callNumber;

                public T Current { get; set; }

                object IEnumerator.Current => this.Current;


                internal Enumerator(IEnumerator<T> inner, int throwAfter)
                {
                    this.inner = inner;
                    this.throwAfter = throwAfter;
                }
                public void Dispose()
                {
                    this.inner.Dispose();
                }

                public bool MoveNext()
                {
                    if (this.callNumber >= this.throwAfter)
                    {
                        throw new InvalidOperationException();
                    }

                    this.callNumber++;

                    if (this.inner.MoveNext())
                    {
                        this.Current = this.inner.Current;
                        return true;
                    }

                    this.Current = default;
                    return false;
                }

                public void Reset()
                {
                    this.inner.Reset();
                }
            }

            private readonly IEnumerable<T> inner;
            private readonly int throwAfter;

            public ThrowsAfterEnumerable(IEnumerable<T> inner, int throwAfter)
            {
                this.inner = inner;
                this.throwAfter = throwAfter;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new Enumerator(this.inner.GetEnumerator(), this.throwAfter);
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

            public Cosmos.Tracing.TraceLevel Level => Cosmos.Tracing.TraceLevel.Off;

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

            public ITrace StartChild(string name, TraceComponent component, Cosmos.Tracing.TraceLevel level)
            {
                return this;
            }
        }

        /// <summary>
        /// ITrace which only traces children and parents.
        /// </summary>
        private sealed class SimpleTrace : ITrace
        {
            public string Name { get; private set; }

            public Guid Id { get; } = Guid.NewGuid();

            public DateTime StartTime { get; } = DateTime.UtcNow;

            public TimeSpan Duration => DateTime.UtcNow - this.StartTime;

            public Cosmos.Tracing.TraceLevel Level { get; private set; }

            public TraceComponent Component { get; private set; }

            public TraceSummary Summary => new TraceSummary();

            public ITrace Parent { get; private set; }

            public IReadOnlyList<ITrace> Children { get; } = new List<ITrace>();

            public IReadOnlyDictionary<string, object> Data { get; } = new Dictionary<string, object>();

            internal SimpleTrace(ITrace parent, string name, TraceComponent component, Cosmos.Tracing.TraceLevel level)
            {
                this.Parent = parent;
                this.Name = name;
                this.Component = component;
                this.Level = level;
            }

            public void AddChild(ITrace trace)
            {
                List<ITrace> children = (List<ITrace>)this.Children;
                lock (children)
                {
                    children.Add(trace);
                }
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
                return this.StartChild(name, TraceComponent.Unknown, Cosmos.Tracing.TraceLevel.Off);
            }

            public ITrace StartChild(string name, TraceComponent component, Cosmos.Tracing.TraceLevel level)
            {
                ITrace child = new SimpleTrace(this, name, component, level);

                List<ITrace> children = (List<ITrace>)this.Children;
                lock (children)
                {
                    children.Add(child);
                }

                return child;
            }
        }

        /// <summary>
        /// Different task counts which explore different code paths.
        /// </summary>
        private static readonly int[] TaskCounts = new[] { 0, 1, 2, 511, 512, 513, 1024, 1025 };

        /// <summary>
        /// Different max concurrencies which explore different code paths.
        /// </summary>
        private static readonly int[] Concurrencies = new[] { 1, 2, 511, 512, 513, int.MaxValue };

        private static readonly ITrace EmptyTrace = new NullTrace();

        [TestMethod]
        public async Task ParameterValidationAsync()
        {
            // test contract for parameters

            ArgumentNullException prefetchersArg = Assert.ThrowsException<ArgumentNullException>(
                () =>
                    ParallelPrefetch.PrefetchInParallelAsync(
                    null,
                    123,
                    EmptyTrace,
                    default));
            Assert.AreEqual("prefetchers", prefetchersArg.ParamName);

            ArgumentNullException traceArg = Assert.ThrowsException<ArgumentNullException>(
                () =>
                    ParallelPrefetch.PrefetchInParallelAsync(
                    Array.Empty<IPrefetcher>(),
                    123,
                    null,
                    default));
            Assert.AreEqual("trace", traceArg.ParamName);

            // maxConcurrency can be < 0 ; check that that doesn't throw
            await ParallelPrefetch.PrefetchInParallelAsync(Array.Empty<IPrefetcher>(), -123, EmptyTrace, default);
        }

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
        public async Task ComplicatedPrefetcherAsync()
        {
            // test that a complicated prefetcher is full started and completed
            //
            // the rest of the tests don't use a completely trivial
            // IPrefetcher, but they are substantially simpler

            foreach (int maxConcurrency in Concurrencies)
            {
                ComplicatedPrefetcher prefetcher = new ComplicatedPrefetcher();

                await ParallelPrefetch.PrefetchInParallelAsync(
                    new IPrefetcher[] { prefetcher },
                    maxConcurrency,
                    EmptyTrace,
                    default);

                prefetcher.AssertCorrect();
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
        public async Task TraceCorrectlyPassedAsync()
        {
            // test that we make ONE ITrace per invocation
            // and that it the returned child trace is correctly
            // passed to all IPrefetchers

            foreach (int maxConcurrency in Concurrencies)
            {
                if (maxConcurrency <= 0)
                {
                    // nothing to do here, we won't do any allocations
                    continue;
                }

                foreach (int taskCount in TaskCounts)
                {
                    using ITrace simpleTrace = new SimpleTrace(null, "Root", TraceComponent.Batch, Cosmos.Tracing.TraceLevel.Off);

                    IEnumerable<IPrefetcher> prefetchers = CreatePrefetchers(taskCount, simpleTrace);

                    await ParallelPrefetch.PrefetchInParallelAsync(
                        prefetchers,
                        maxConcurrency,
                        simpleTrace,
                        default);

                    // our prefetchers don't create any children, but we expect one
                    // to be created by ParallelPrefetch
                    Assert.AreEqual(1, simpleTrace.Children.Count);
                    Assert.AreEqual(0, simpleTrace.Children[0].Children.Count);

                    // the one trace we start has a well known set of attributes, so check them
                    Assert.AreEqual("Prefetching", simpleTrace.Children[0].Name);
                    Assert.AreEqual(TraceComponent.Pagination, simpleTrace.Children[0].Component);
                    Assert.AreEqual(Cosmos.Tracing.TraceLevel.Info, simpleTrace.Children[0].Level);
                }
            }

            static IEnumerable<IPrefetcher> CreatePrefetchers(int count, ITrace expectedParentTrace)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new ExpectedParentTracePrefetcher(expectedParentTrace);
                }
            }
        }

        [TestMethod]
        public async Task RentedBuffersAllReturnedAsync()
        {
            // test that all rented buffers are correctly returned
            // (and in the expected state)

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
                        ValidatingRandomizedArrayPool<object> objectPool = new ValidatingRandomizedArrayPool<object>("unexpected value");

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

        [TestMethod]
        public async Task TaskExceptionsHandledAsync()
        {
            // test that raising exceptions during processing tasks
            // doesn't leak or otherwise fail

            Task faultedTask = Task.FromException(new NotSupportedException());

            try
            {
                foreach (int maxConcurrency in Concurrencies)
                {
                    if (maxConcurrency <= 1)
                    {
                        // we won't do anything fancy, so skip
                        continue;
                    }

                    foreach (int taskCount in TaskCounts)
                    {
                        if (taskCount <= 1)
                        {
                            // we won't do anything fancy, so skip
                            continue;
                        }

                        for (int faultOnTask = 0; faultOnTask < taskCount; faultOnTask++)
                        {
                            IEnumerable<IPrefetcher> prefetchers = CreatePrefetchers(taskCount, faultOnTask, static () => { }, static () => { });

                            ValidatingRandomizedArrayPool<IPrefetcher> prefetcherPool = new ValidatingRandomizedArrayPool<IPrefetcher>(new ThrowsPrefetcher());
                            ValidatingRandomizedArrayPool<Task> taskPool = new ValidatingRandomizedArrayPool<Task>(faultedTask);
                            ValidatingRandomizedArrayPool<object> objectPool = new ValidatingRandomizedArrayPool<object>("unexpected value");

                            ParallelPrefetch.ParallelPrefetchTestConfig config =
                                new ParallelPrefetch.ParallelPrefetchTestConfig(
                                    prefetcherPool,
                                    taskPool,
                                    objectPool
                                );

                            Exception caught = null;
                            try
                            {
                                await ParallelPrefetch.PrefetchInParallelImplAsync(
                                    prefetchers,
                                    maxConcurrency,
                                    EmptyTrace,
                                    config,
                                    default);
                            }
                            catch (Exception e)
                            {
                                caught = e;
                            }

                            Assert.IsNotNull(caught, $"concurrency={maxConcurrency}, tasks={taskCount}, faultOn={faultOnTask} - didn't produce exception as expected");

                            // buffer management can't break in the face of errors, so check here too
                            prefetcherPool.AssertAllReturned();
                            taskPool.AssertAllReturned();
                            objectPool.AssertAllReturned();
                        }
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

            static IEnumerable<IPrefetcher> CreatePrefetchers(int count, int faultOnTask, Action beforeAwait, Action afterAwait)
            {
                for (int i = 0; i < count; i++)
                {
                    if (faultOnTask == i)
                    {
                        yield return new ThrowsPrefetcher();
                    }
                    else
                    {
                        yield return new TestOncePrefetcher(beforeAwait, afterAwait);
                    }
                }
            }
        }

        [TestMethod]
        public async Task EnumerableExceptionsHandledAsync()
        {
            // test that raising exceptions during enumeration
            // doesn't leak or otherwise fail

            Task faultedTask = Task.FromException(new NotSupportedException());

            try
            {
                foreach (int maxConcurrency in Concurrencies.Reverse())
                {
                    if (maxConcurrency <= 1)
                    {
                        // we won't do anything fancy, so skip
                        continue;
                    }

                    foreach (int taskCount in TaskCounts)
                    {
                        for (int faultAfter = 0; faultAfter < taskCount; faultAfter++)
                        {
                            IEnumerable<IPrefetcher> prefetchersRaw = CreatePrefetchers(taskCount, faultAfter, static () => { }, static () => { });
                            IEnumerable<IPrefetcher> prefetchers = new ThrowsAfterEnumerable<IPrefetcher>(prefetchersRaw, faultAfter);

                            ValidatingRandomizedArrayPool<IPrefetcher> prefetcherPool = new ValidatingRandomizedArrayPool<IPrefetcher>(new ThrowsPrefetcher());
                            ValidatingRandomizedArrayPool<Task> taskPool = new ValidatingRandomizedArrayPool<Task>(faultedTask);
                            ValidatingRandomizedArrayPool<object> objectPool = new ValidatingRandomizedArrayPool<object>("unexpected value");

                            ParallelPrefetch.ParallelPrefetchTestConfig config =
                                new ParallelPrefetch.ParallelPrefetchTestConfig(
                                    prefetcherPool,
                                    taskPool,
                                    objectPool
                                );

                            Exception caught = null;
                            try
                            {
                                await ParallelPrefetch.PrefetchInParallelImplAsync(
                                    prefetchers,
                                    maxConcurrency,
                                    EmptyTrace,
                                    config,
                                    default);
                            }
                            catch (Exception e)
                            {
                                caught = e;
                            }

                            Assert.IsNotNull(caught, $"concurrency={maxConcurrency}, tasks={taskCount}, faultAfter={faultAfter} - didn't produce exception as expected");

                            // buffer management can't break in the face of errors, so check here too
                            prefetcherPool.AssertAllReturned();
                            taskPool.AssertAllReturned();
                            objectPool.AssertAllReturned();
                        }
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

            static IEnumerable<IPrefetcher> CreatePrefetchers(int count, int faultOnTask, Action beforeAwait, Action afterAwait)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new TestOncePrefetcher(beforeAwait, afterAwait);
                }
            }
        }

        [TestMethod]
        public async Task CancellationHandledAsync()
        {
            // cancellation is expensive, so rather than check every 
            // cancellation point - we just probe some proportional
            // to this constant
            const int StepRatio = 10;

            // test that cancellation during processing 
            // doesn't leak or otherwise fail

            Task faultedTask = Task.FromException(new NotSupportedException());

            try
            {
                foreach (int maxConcurrency in Concurrencies)
                {
                    if (maxConcurrency <= 1)
                    {
                        // we won't do anything fancy, so skip
                        continue;
                    }

                    foreach (int taskCount in TaskCounts)
                    {
                        if (taskCount <= 1)
                        {
                            // we won't do anything fancy, so skip
                            continue;
                        }

                        int step = Math.Max(1, taskCount / StepRatio);

                        for (int cancelBeforeTask = 0; cancelBeforeTask < taskCount; cancelBeforeTask += step)
                        {
                            using CancellationTokenSource cts = new();

                            int startedBeforeCancellation = 0;
                            object sync = new object();

                            IEnumerable<IPrefetcher> prefetchers =
                                CreatePrefetchers(
                                    taskCount,
                                    () =>
                                    {
                                        if (!cts.IsCancellationRequested)
                                        {
                                            int newValue = Interlocked.Increment(ref startedBeforeCancellation);

                                            if (newValue >= cancelBeforeTask)
                                            {
                                                cts.Cancel();
                                            }
                                        }
                                    },
                                    () => { });

                            ValidatingRandomizedArrayPool<IPrefetcher> prefetcherPool = new ValidatingRandomizedArrayPool<IPrefetcher>(new ThrowsPrefetcher());
                            ValidatingRandomizedArrayPool<Task> taskPool = new ValidatingRandomizedArrayPool<Task>(faultedTask);
                            ValidatingRandomizedArrayPool<object> objectPool = new ValidatingRandomizedArrayPool<object>("unexpected value");

                            ParallelPrefetch.ParallelPrefetchTestConfig config =
                                new ParallelPrefetch.ParallelPrefetchTestConfig(
                                    prefetcherPool,
                                    taskPool,
                                    objectPool
                                );

                            Exception caught = null;
                            try
                            {
                                await ParallelPrefetch.PrefetchInParallelImplAsync(
                                    prefetchers,
                                    maxConcurrency,
                                    EmptyTrace,
                                    config,
                                    cts.Token);
                            }
                            catch (Exception e)
                            {
                                caught = e;
                            }

                            Assert.IsNotNull(caught, $"concurrency={maxConcurrency}, tasks={taskCount}, cancelBeforeTask={cancelBeforeTask} - didn't produce exception as expected");

                            // we might burst above this, but we should always at least _reach_ it
                            Assert.IsTrue(cancelBeforeTask <= startedBeforeCancellation, $"{cancelBeforeTask} > {startedBeforeCancellation} ; we should have reach our cancellation point");

                            Assert.IsTrue(caught is OperationCanceledException);

                            // buffer management can't break in the face of cancellation, so check here too
                            prefetcherPool.AssertAllReturned();
                            taskPool.AssertAllReturned();
                            objectPool.AssertAllReturned();
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
        }
    }
}
