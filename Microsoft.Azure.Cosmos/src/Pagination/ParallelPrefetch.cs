// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal static class ParallelPrefetch
    {
        /// <summary>
        /// Common state that is needed for all tasks started via <see cref="PrefetchInParallelAsync(IEnumerable{IPrefetcher}, int, ITrace, CancellationToken)"/>, unless
        /// certain special cases hold.
        /// 
        /// Also used as a synchronization primitive.
        /// </summary>
        private sealed class CommonPrefetchState
        {
            // we also use this to signal if we're finished enumerating, to save space
            private IEnumerator<IPrefetcher> enumerator;

            /// <summary>
            /// If this is true, it's a signal that new work should not be queued up.
            /// </summary>
            internal bool FinishedEnumerating
            => Volatile.Read(ref this.enumerator) == null;

            /// <summary>
            /// Common <see cref="ITrace"/> to be used by all tasks.
            /// </summary>
            internal ITrace PrefetchTrace { get; private set; }

            /// <summary>
            /// The <see cref="IEnumerator{T}"/> which will produce the next <see cref="IPrefetcher"/>
            /// to use.
            /// 
            /// Once at least one Task been started, should only be accessed under a lock.
            /// 
            /// If <see cref="FinishedEnumerating"/> == true, this returns null.
            /// </summary>
            internal IEnumerator<IPrefetcher> Enumerator
            => Volatile.Read(ref this.enumerator);

            /// <summary>
            /// <see cref="CancellationToken"/> provided via <see cref="PrefetchInParallelAsync(IEnumerable{IPrefetcher}, int, ITrace, CancellationToken)"/>.
            /// </summary>
            internal CancellationToken CancellationToken { get; private set; }

            internal CommonPrefetchState(ITrace prefetchTrace, IEnumerator<IPrefetcher> enumerator, CancellationToken cancellationToken)
            {
                this.PrefetchTrace = prefetchTrace;
                this.enumerator = enumerator;
                this.CancellationToken = cancellationToken;
            }

            /// <summary>
            /// Cause <see cref="FinishedEnumerating"/> to return true.
            /// </summary>
            internal void SetFinishedEnumerating()
            {
                Volatile.Write(ref this.enumerator, null);
            }
        }

        /// <summary>
        /// State passed when we start a Task with an initial <see cref="IPrefetcher"/>.
        /// 
        /// That started Task will obtain it's next IPrefetchers using the <see cref="CommonPrefetchState"/>
        /// that is also provided.
        /// </summary>
        private sealed class SinglePrefetchState
        {
            /// <summary>
            /// State common to the whole <see cref="PrefetchInParallelAsync"/> call.
            /// </summary>
            internal CommonPrefetchState CommonState { get; private set; }

            /// <summary>
            /// <see cref="IPrefetcher"/> which must be invoked next.
            /// </summary>
            internal IPrefetcher CurrentPrefetcher { get; set; }

            internal SinglePrefetchState(CommonPrefetchState commonState, IPrefetcher initialPrefetcher)
            {
                this.CommonState = commonState;
                this.CurrentPrefetcher = initialPrefetcher;
            }
        }

        /// <summary>
        /// For testing purposes, provides ways to instrument <see cref="PrefetchInParallelAsync(IEnumerable{IPrefetcher}, int, ITrace, CancellationToken)"/>.
        /// 
        /// You shouldn't be using this outside of test projects.
        /// </summary>
        internal sealed class ParallelPrefetchTestConfig
        {
            internal ArrayPool<IPrefetcher> PrefetcherPool { get; private set; }
            internal ArrayPool<Task> TaskPool { get; private set; }
            internal ArrayPool<object> ObjectPool { get; private set; }

            internal ParallelPrefetchTestConfig(
                ArrayPool<IPrefetcher> prefetcherPool,
                ArrayPool<Task> taskPool,
                ArrayPool<object> objectPool)
            {
                this.PrefetcherPool = prefetcherPool;
                this.TaskPool = taskPool;
                this.ObjectPool = objectPool;
            }
        }

        /// <summary>
        /// Number of tasks started at one time, maximum, when working through prefetchers.
        /// 
        /// Also used as a the limit between Low and High concurrency implementations.
        /// 
        /// This number should be reasonable large, but less than the point where a 
        /// Task[BatchLimit] ends up on the LOH (which will be around 8,192).
        /// </summary>
        private const int BatchLimit = 512;

        public static Task PrefetchInParallelAsync(
            IEnumerable<IPrefetcher> prefetchers,
            int maxConcurrency,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            prefetchers = prefetchers ?? throw new ArgumentNullException(nameof(prefetchers));
            trace = trace ?? throw new ArgumentNullException(nameof(trace));

            return PrefetchInParallelImplAsync(prefetchers, maxConcurrency, trace, null, cancellationToken);
        }

        /// <summary>
        /// Exposed for testing purposes, do not call directly.
        /// </summary>
        internal static Task PrefetchInParallelImplAsync(
            IEnumerable<IPrefetcher> prefetchers,
            int maxConcurrency,
            ITrace trace,
            ParallelPrefetchTestConfig config,
            CancellationToken cancellationToken)
        {
            if (maxConcurrency <= 0)
            {
                // old code would just... allocate and then do nothing
                //
                // so we do nothing here, for compatability purposes
                return Task.CompletedTask;
            }
            else if (maxConcurrency == 1)
            {
                // we don't pass config here because this special case has no renting
                return SingleConcurrencyPrefetchInParallelAsync(prefetchers, trace, cancellationToken);
            }
            else if (maxConcurrency <= BatchLimit)
            {
                return LowConcurrencyPrefetchInParallelAsync(prefetchers, maxConcurrency, trace, config, cancellationToken);
            }
            else
            {
                return HighConcurrencyPrefetchInParallelAsync(prefetchers, maxConcurrency, trace, config, cancellationToken);
            }
        }

        /// <summary>
        /// Shared code for starting traces while prefetching.
        /// </summary>
        private static ITrace CommonStartTrace(ITrace trace)
        {
            return trace.StartChild(name: "Prefetching", TraceComponent.Pagination, TraceLevel.Info);
        }

        /// <summary>
        /// Helper for grabbing a reusable array.
        /// </summary>
        private static T[] RentArray<T>(ParallelPrefetchTestConfig config, int minSize, bool clear)
        {
            T[] ret;
            if (config != null)
            {
#pragma warning disable IDE0045 // Convert to conditional expression - chained else if is clearer
                if (typeof(T) == typeof(IPrefetcher))
                {
                    ret = (T[])(object)config.PrefetcherPool.Rent(minSize);
                }
                else if (typeof(T) == typeof(Task))
                {
                    ret = (T[])(object)config.TaskPool.Rent(minSize);
                }
                else
                {
                    ret = (T[])(object)config.ObjectPool.Rent(minSize);
                }
#pragma warning restore IDE0045
            }
            else
            {
                ret = ArrayPool<T>.Shared.Rent(minSize);
            }

            if (clear)
            {
                Array.Clear(ret, 0, ret.Length);
            }

            return ret;
        }

        /// <summary>
        /// Helper for returning arrays what were rented via <see cref="RentArray"/>.
        /// </summary>
        private static void ReturnRentedArray<T>(ParallelPrefetchTestConfig config, T[] array, int clearThrough)
        {
            if (array == null)
            {
                return;
            }

            // this is important, otherwise we might leave Tasks and IPrefetchers
            // rooted long enough to cause problems
            Array.Clear(array, 0, clearThrough);

            if (config != null)
            {
                if (typeof(T) == typeof(IPrefetcher))
                {
                    config.PrefetcherPool.Return((IPrefetcher[])(object)array);
                }
                else if (typeof(T) == typeof(Task))
                {
                    config.TaskPool.Return((Task[])(object)array);
                }
                else
                {
                    config.ObjectPool.Return((object[])(object)array);
                }
            }
            else
            {
                ArrayPool<T>.Shared.Return(array);
            }
        }

        /// <summary>
        /// Starts a new Task that first calls <see cref="IPrefetcher.PrefetchAsync(ITrace, CancellationToken)"/> on the passed
        /// <see cref="IPrefetcher"/>, and then grabs new ones from <see cref="CommonPrefetchState.Enumerator"/> and repeats the process
        /// until either the enumerator finishes or something sets <see cref="CommonPrefetchState.FinishedEnumerating"/>.
        /// </summary>
        private static Task CommonStartTaskAsync(CommonPrefetchState commonState, IPrefetcher firstPrefetcher)
        {
            SinglePrefetchState state = new SinglePrefetchState(commonState, firstPrefetcher);

            // this is mimicing the behavior of Task.Run(...) (that is, default CancellationToken, default Scheduler, DenyAttachChild, etc.)
            // but in a way that let's us pass a context object
            //
            // this lets us declare a static delegate, and thus let's compiler reuse the delegate allocation
            Task<Task> taskLoop =
                Task<Task>.Factory.StartNew(
                    static async (context) =>
                    {
                        // this method is structured a bit oddly to prevent the compiler from putting more data into the 
                        // state of the Task - basically, don't have any locals (except context) that survive across an await
                        //
                        // we could go harder here and just not use async/await but that's awful for maintainability
                        try
                        {
                            while (true)
                            {
                                // step up to the initial await
                                {
                                    SinglePrefetchState innerState = (SinglePrefetchState)context;

                                    CommonPrefetchState innerCommonState = innerState.CommonState;
                                    (ITrace prefetchTrace, CancellationToken cancellationToken) = (innerCommonState.PrefetchTrace, innerCommonState.CancellationToken);
                                    await innerState.CurrentPrefetcher.PrefetchAsync(prefetchTrace, cancellationToken);
                                }

                                // step for preparing the next prefetch
                                {
                                    SinglePrefetchState innerState = (SinglePrefetchState)context;

                                    CommonPrefetchState innerCommonState = innerState.CommonState;

                                    if (innerCommonState.FinishedEnumerating)
                                    {
                                        // we're done, bail
                                        return;
                                    }

                                    // proceed to the next item
                                    //
                                    // we need this lock because at this point there
                                    // are other Tasks potentially also looking to call
                                    // e.MoveNext()
                                    lock (innerCommonState)
                                    {
                                        // this can have transitioned to null since we last checked
                                        // so this is basically double-check locking
                                        IEnumerator<IPrefetcher> e = innerCommonState.Enumerator;
                                        if (e == null)
                                        {
                                            return;
                                        }

                                        if (!e.MoveNext())
                                        {
                                            // we're done, signal to every other task to also bail
                                            innerCommonState.SetFinishedEnumerating();

                                            return;
                                        }

                                        // move on to the new IPrefetcher just obtained
                                        innerState.CurrentPrefetcher = e.Current;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            SinglePrefetchState innerState = (SinglePrefetchState)context;

                            // some error was encountered, we should tell other tasks to stop starting new prefetch tasks
                            // because we're about to cancel
                            innerState.CommonState.SetFinishedEnumerating();

                            // percolate the error up
                            throw;
                        }
                    },
                    state,
                    default,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);

            // we _could_ maybe optimize this more... perhaps using a SemaphoreSlim or something
            // but that complicates error reporting and is also awful for maintability
            Task unwrapped = taskLoop.Unwrap();

            return unwrapped;
        }

        /// <summary>
        /// Fills a portion of an IPrefetcher[] using the passed enumerator.
        /// 
        /// Returns the index that would next be filled.
        /// 
        /// Updates the passed <see cref="CommonPrefetchState"/> if the end of the enumerator is reached.
        /// 
        /// Synchronization is the concern of the caller, not this method.
        /// </summary>
        private static int FillPrefetcherBuffer(CommonPrefetchState commonState, IPrefetcher[] prefetchers, int startIndex, int endIndex, IEnumerator<IPrefetcher> e)
        {
            int curIndex;
            for (curIndex = startIndex; curIndex < endIndex; curIndex++)
            {
                if (!e.MoveNext())
                {
                    commonState.SetFinishedEnumerating();
                    break;
                }

                prefetchers[curIndex] = e.Current;
            }

            return curIndex;
        }

        /// <summary>
        /// Special case for when maxConcurrency == 1.
        /// 
        /// This devolves into a foreach loop.
        /// </summary>
        private static async Task SingleConcurrencyPrefetchInParallelAsync(IEnumerable<IPrefetcher> prefetchers, ITrace trace, CancellationToken cancellationToken)
        {
            using (ITrace prefetchTrace = CommonStartTrace(trace))
            {
                foreach (IPrefetcher prefetcher in prefetchers)
                {
                    await prefetcher.PrefetchAsync(prefetchTrace, cancellationToken);
                }
            }
        }

        /// <summary>
        /// The case where maxConcurrency is less than or equal to BatchLimit.
        /// 
        /// This starts up to maxConcurrency simultanous Tasks, doing so in a way that
        /// requires rented arrays of maxConcurrency size.
        /// </summary>
        private static async Task LowConcurrencyPrefetchInParallelAsync(
            IEnumerable<IPrefetcher> prefetchers,
            int maxConcurrency,
            ITrace trace,
            ParallelPrefetchTestConfig config,
            CancellationToken cancellationToken)
        {
            IPrefetcher[] initialPrefetchers = null;
            Task[] runningTasks = null;

            int nextPrefetcherIndex = 0;
            int nextRunningTaskIndex = 0;

            try
            {
                using (ITrace prefetchTrace = CommonStartTrace(trace))
                {
                    using (IEnumerator<IPrefetcher> e = prefetchers.GetEnumerator())
                    {
                        if (!e.MoveNext())
                        {
                            // literally nothing to prefetch
                            return;
                        }

                        IPrefetcher first = e.Current;

                        if (!e.MoveNext())
                        {
                            // special case: a single prefetcher... just await it, and skip all the heavy work
                            await first.PrefetchAsync(prefetchTrace, cancellationToken);
                            return;
                        }

                        // need to actually do things to start prefetching in parallel
                        // so grab some state and stash the first two prefetchers off

                        initialPrefetchers = RentArray<IPrefetcher>(config, maxConcurrency, clear: false);
                        initialPrefetchers[0] = first;
                        initialPrefetchers[1] = e.Current;

                        CommonPrefetchState commonState = new CommonPrefetchState(prefetchTrace, e, cancellationToken);

                        // batch up a bunch of IPrefetchers to kick off
                        // 
                        // we do this separately from starting the Tasks so we can avoid a lock
                        // and quicky get to maxConcurrency degrees of parallelism
                        nextPrefetcherIndex = FillPrefetcherBuffer(commonState, initialPrefetchers, 2, maxConcurrency, e);

                        // actually start all the tasks, stashing them in a rented Task[]
                        runningTasks = RentArray<Task>(config, nextPrefetcherIndex, clear: false);

                        for (nextRunningTaskIndex = 0; nextRunningTaskIndex < nextPrefetcherIndex; nextRunningTaskIndex++)
                        {
                            IPrefetcher toStart = initialPrefetchers[nextRunningTaskIndex];
                            Task startedTask = CommonStartTaskAsync(commonState, toStart);

                            runningTasks[nextRunningTaskIndex] = startedTask;
                        }

                        // hand the prefetcher array back early, so other callers can use it
                        ReturnRentedArray(config, initialPrefetchers, nextPrefetcherIndex);
                        initialPrefetchers = null;

                        // now await all Tasks in turn
                        for (int toAwaitTaskIndex = 0; toAwaitTaskIndex < nextRunningTaskIndex; toAwaitTaskIndex++)
                        {
                            Task toAwait = runningTasks[toAwaitTaskIndex];

                            try
                            {
                                await toAwait;
                            }
                            catch
                            {
                                // if we encountered some exception, tell the remaining tasks to bail
                                // the next time they check commonState
                                commonState.SetFinishedEnumerating();

                                // we still need to observe all the tasks we haven't yet to avoid an UnobservedTaskException
                                for (int awaitAndIgnoreTaskIndex = toAwaitTaskIndex + 1; awaitAndIgnoreTaskIndex < nextRunningTaskIndex; awaitAndIgnoreTaskIndex++)
                                {
                                    try
                                    {
                                        await runningTasks[awaitAndIgnoreTaskIndex];
                                    }
                                    catch
                                    {
                                        // intentionally left empty, we swallow all errors after the first
                                    }
                                }

                                throw;
                            }
                        }
                    }
                }
            }
            finally
            {
                ReturnRentedArray(config, initialPrefetchers, nextPrefetcherIndex);
                ReturnRentedArray(config, runningTasks, nextRunningTaskIndex);
            }
        }

        /// <summary>
        /// The case where maxConcurrency is greater than BatchLimit.
        /// 
        /// This starts up to maxConcurrency simultanous Tasks, doing so in batches
        /// of BatchLimit (or less) size.  Active Tasks are tracked in a psuedo-linked-list
        /// over rented object[].
        /// 
        /// This is more complicated, less likely to hit maxConcurrency degrees of
        /// parallelism, and less allocation efficient when compared to LowConcurrencyPrefetchInParallelAsync.
        /// 
        /// However, it doesn't allocate gigantic arrays and doesn't wait for full enumeration
        /// before starting to prefetch.
        /// </summary>
        private static async Task HighConcurrencyPrefetchInParallelAsync(
            IEnumerable<IPrefetcher> prefetchers,
            int maxConcurrency,
            ITrace trace,
            ParallelPrefetchTestConfig config,
            CancellationToken cancellationToken)
        {
            IPrefetcher[] currentBatch = null;

            // this ends up holding a sort of linked list where
            // each entry is actually a Task until the very last one
            // which is an object[]
            //
            // as soon as a null is encountered, either where a Task or
            // an object[] is expected, the linked list is done
            object[] runningTasks = null;

            try
            {
                using (ITrace prefetchTrace = CommonStartTrace(trace))
                {
                    using (IEnumerator<IPrefetcher> e = prefetchers.GetEnumerator())
                    {
                        if (!e.MoveNext())
                        {
                            // no prefetchers at all
                            return;
                        }

                        IPrefetcher first = e.Current;

                        if (!e.MoveNext())
                        {
                            // special case: a single prefetcher... just await it, and skip all the heavy work
                            await first.PrefetchAsync(prefetchTrace, cancellationToken);
                            return;
                        }

                        // need to actually do things to start prefetching in parallel
                        // so grab some state and stash the first two prefetchers off

                        currentBatch = RentArray<IPrefetcher>(config, BatchLimit, clear: false);
                        currentBatch[0] = first;
                        currentBatch[1] = e.Current;

                        // we need this all null because we use null as a stopping condition later
                        runningTasks = RentArray<object>(config, BatchLimit, clear: true);

                        CommonPrefetchState commonState = new CommonPrefetchState(prefetchTrace, e, cancellationToken);

                        // what we do here is buffer up to BatchLimit IPrefetchers to start
                        // and then... start them all
                        //
                        // we stagger this so we quickly get a bunch of tasks started without spending too
                        // much time pre-loading everything

                        // first we grab a bunch of prefetchers outside of the lock
                        //
                        // we know that maxConcurrency > BatchLimit, so can just pass it as our cutoff here
                        int bufferedPrefetchers = FillPrefetcherBuffer(commonState, currentBatch, 2, BatchLimit, e);

                        int nextChunkIndex = 0;
                        object[] currentChunk = runningTasks;

                        int remainingConcurrency = maxConcurrency;

                        // if we encounter any error, we remember it
                        // but as soon as we start a single task we've got
                        // to see most of this code through so we observe all of them
                        ExceptionDispatchInfo capturedException = null;

                        while (true)
                        {
                            // start and store the last set of Tasks we got from FillPrefetcherBuffer
                            for (int toStartIx = 0; toStartIx < bufferedPrefetchers; toStartIx++)
                            {
                                IPrefetcher prefetcher = currentBatch[toStartIx];
                                Task startedTask = CommonStartTaskAsync(commonState, prefetcher);

                                currentChunk[nextChunkIndex] = startedTask;
                                nextChunkIndex++;

                                // do we need a new slab to store tasks in?
                                if (nextChunkIndex == currentChunk.Length - 1)
                                {
                                    // we need this all null because we use null as a stopping condition later
                                    object[] newChunk = RentArray<object>(config, BatchLimit, clear: true);

                                    currentChunk[currentChunk.Length - 1] = newChunk;

                                    currentChunk = newChunk;
                                    nextChunkIndex = 0;
                                }
                            }

                            remainingConcurrency -= bufferedPrefetchers;

                            // check to see if we've started all the concurrent Tasks we can
                            if (remainingConcurrency == 0)
                            {
                                break;
                            }

                            int nextBatchSizeLimit = remainingConcurrency < BatchLimit ? remainingConcurrency : BatchLimit;

                            // if one of the previously started Tasks exhausted the enumerator
                            // we're done, even if we still have space
                            if (commonState.FinishedEnumerating)
                            {
                                break;
                            }

                            // now that Tasks have started, we MUST synchronize access to 
                            // the enumerator
                            lock (commonState)
                            {
                                // the answer might have changed, so we double-check
                                // this once we've got the lock
                                if (commonState.FinishedEnumerating)
                                {
                                    break;
                                }

                                // grab the next set of prefetchers to start
                                try
                                {
                                    bufferedPrefetchers = FillPrefetcherBuffer(commonState, currentBatch, 0, nextBatchSizeLimit, e);
                                }
                                catch (Exception exc)
                                {
                                    // this can get raised if the enumerator faults
                                    //
                                    // in this case we might have some tasks started, and so we need to _stop_ starting new tasks but
                                    // still move on to observing everything we've already started

                                    commonState.SetFinishedEnumerating();
                                    capturedException = ExceptionDispatchInfo.Capture(exc);

                                    break;
                                }
                            }

                            // if we got nothing back, we can break right here
                            if (bufferedPrefetchers == 0)
                            {
                                break;
                            }
                        }

                        // hand the prefetch array back, we're done with it
                        ReturnRentedArray(config, currentBatch, BatchLimit);
                        currentBatch = null;

                        // now wait for all the tasks to complete
                        //
                        // we walk through all of them, even if we encounter an error
                        // because we need to walk the whole linked-list and this is
                        // simpler than an explicit error code path

                        int toAwaitIndex = 0;
                        while (runningTasks != null)
                        {
                            Task toAwait = (Task)runningTasks[toAwaitIndex];

                            // are we done?
                            if (toAwait == null)
                            {
                                // hand the last of the arrays back
                                ReturnRentedArray(config, runningTasks, toAwaitIndex);
                                runningTasks = null;

                                break;
                            }

                            try
                            {
                                await toAwait;
                            }
                            catch (Exception ex)
                            {
                                if (capturedException == null)
                                {
                                    // if we encountered some exception, tell the remaining tasks to bail
                                    // the next time they check commonState
                                    commonState.SetFinishedEnumerating();

                                    // save the exception so we can rethrow it later
                                    capturedException = ExceptionDispatchInfo.Capture(ex);
                                }
                            }

                            // advance, moving to the next chunk if we've hit that limit
                            toAwaitIndex++;

                            if (toAwaitIndex == runningTasks.Length - 1)
                            {
                                object[] oldChunk = runningTasks;

                                runningTasks = (object[])runningTasks[runningTasks.Length - 1];
                                toAwaitIndex = 0;

                                // we're done with this, let some other caller reuse it immediately
                                ReturnRentedArray(config, oldChunk, oldChunk.Length);
                            }
                        }

                        // fault, if any task failed, after we've finished cleaning up
                        capturedException?.Throw();
                    }
                }
            }
            finally
            {
                // cleanup if something went wrong while these were still rented
                //
                // this can basically only happen if the enumerator itself faults
                // which is unlikely, but far from impossible

                ReturnRentedArray(config, currentBatch, BatchLimit);

                while (runningTasks != null)
                {
                    object[] oldChunk = runningTasks;

                    runningTasks = (object[])runningTasks[runningTasks.Length - 1];

                    ReturnRentedArray(config, oldChunk, oldChunk.Length);
                }
            }
        }
    }
}
