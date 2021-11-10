//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// This is a thread safe AsyncCache that allows refreshing values in the background.
    /// The benefits of AsyncCacheNonBlocking over AsyncCache is it keeps stale values until the refresh is completed. 
    /// AsyncCache removes values causing it to block all requests until the refresh is complete.
    /// 1. For example 1 replica moved out of the 4 replicas available. 3 replicas could still be processing requests.
    ///    The request going to the 1 stale replica would be retried.
    /// 2. AsyncCacheNonBlocking updates the value in the cache rather than recreating it on each refresh. This will help reduce object creation.
    /// </summary>
    internal sealed class AsyncCacheNonBlocking<TKey, TValue> : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ConcurrentDictionary<TKey, AsyncLazyWithRefreshTask<TValue>> values;

        private readonly IEqualityComparer<TValue> valueEqualityComparer;
        private readonly IEqualityComparer<TKey> keyEqualityComparer;
        private bool isDisposed;

        public AsyncCacheNonBlocking(
            IEqualityComparer<TValue> valueEqualityComparer,
            IEqualityComparer<TKey> keyEqualityComparer = null)
        {
            this.keyEqualityComparer = keyEqualityComparer ?? EqualityComparer<TKey>.Default;
            this.values = new ConcurrentDictionary<TKey, AsyncLazyWithRefreshTask<TValue>>(this.keyEqualityComparer);
            this.valueEqualityComparer = valueEqualityComparer;
        }

        public AsyncCacheNonBlocking()
            : this(valueEqualityComparer: EqualityComparer<TValue>.Default, keyEqualityComparer: null)
        {
        }

        /// <summary>
        /// <para>
        /// Gets value corresponding to <paramref name="key"/>.
        /// </para>
        /// <para>
        /// If another initialization function is already running, new initialization function will not be started.
        /// The result will be result of currently running initialization function.
        /// </para>
        /// <para>
        /// If previous initialization function is successfully completed it will return the value. It is possible this
        /// value is stale and will only be updated after the force refresh task is complete.
        /// </para>
        /// <para>
        /// Force refresh is true:
        /// If the key does not exist: It will create and await the new task
        /// If the key exists and the current task is still running: It will return the existing task
        /// If the key exists and the current task is already done: It will start a new task to get the updated values. 
        ///     Once the refresh task is complete it will be returned to caller. 
        ///     If it is a success the value in the cache will be updated. If the refresh task throws an exception the key will be removed from the cache. 
        /// </para>
        /// <para>
        /// If previous initialization function failed - new one will be launched.
        /// </para>
        /// </summary>
        public async Task<TValue> GetAsync(
           TKey key,
           Func<Task<TValue>> singleValueInitFunc,
           bool forceRefresh)
        {
            if (this.values.TryGetValue(key, out AsyncLazyWithRefreshTask<TValue> initialLazyValue))
            {
                if (!forceRefresh)
                {
                    return await initialLazyValue.GetValueAsync();
                }

                return await initialLazyValue.CreateAndWaitForBackgroundRefreshTaskAsync(
                    singleValueInitFunc,
                    () => this.TryRemove(key));
            }

            // The AsyncLazyWithRefreshTask is lazy and won't create the task until GetValue is called.
            // It's possible multiple threads will call the GetOrAdd for the same key. The current asyncLazy may
            // not be used if another thread adds it first.
            AsyncLazyWithRefreshTask<TValue> asyncLazy = new AsyncLazyWithRefreshTask<TValue>(singleValueInitFunc, this.cancellationTokenSource.Token);
            AsyncLazyWithRefreshTask<TValue> result = this.values.GetOrAdd(
                key,
                asyncLazy);

            // Another thread async lazy was inserted. Just await on the inserted lazy object.
            if (!object.ReferenceEquals(asyncLazy, result))
            {
                return await result.GetValueAsync();
            }

            // This means the current caller async lazy was inserted into the concurrent dictionary.
            // The task is now awaited on so if an exception occurs it can be removed from
            // the concurrent dictionary.
            try
            {
                return await result.GetValueAsync();
            }
            catch (Exception e)
            {
                DefaultTrace.TraceError(
                            "AsyncCacheNonBlocking Failed GetAsync with key: {0}, Exception: {1}",
                            key.ToString(),
                            e.ToString());

                // Remove the failed task from the dictionary so future requests can send other calls..
                this.values.TryRemove(key, out _);
                throw;
            }
        }

        public void Set(
           TKey key,
           TValue value)
        {
            AsyncLazyWithRefreshTask<TValue> updateValue = new AsyncLazyWithRefreshTask<TValue>(value, this.cancellationTokenSource.Token);
            this.values.AddOrUpdate(
                key,
                updateValue,
                (key, originalValue) => updateValue);
        }

        public bool TryRemove(TKey key)
        {
            return this.values.TryRemove(key, out _);
        }

        /// <summary>
        /// This is AsyncLazy that has an additional Task that can
        /// be used to update the value. This allows concurrent requests
        /// to use the stale value while the refresh is occurring. 
        /// </summary>
        private sealed class AsyncLazyWithRefreshTask<T>
        {
            private readonly CancellationToken cancellationToken;
            private readonly Func<Task<T>> createValueFunc;
            private readonly object valueLock = new object();
            private Task<T> value;
            private Task<T> refreshInProgress;

            public AsyncLazyWithRefreshTask(
                T value,
                CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
                this.createValueFunc = null;
                this.value = Task.FromResult(value);
                this.refreshInProgress = null;
            }

            public AsyncLazyWithRefreshTask(
                Func<Task<T>> taskFactory,
                CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
                this.createValueFunc = taskFactory;
                this.value = null;
                this.refreshInProgress = null;
            }

            public bool IsValueCreated => this.value != null;

            public Task<T> GetValueAsync()
            {
                // The task was already created so just return it.
                Task<T> valueSnapshot = this.value;
                if (valueSnapshot != null)
                {
                    return valueSnapshot;
                }

                // Avoid creating a task if the cancellationToken has been canceled. 
                this.cancellationToken.ThrowIfCancellationRequested();

                lock (this.valueLock)
                {
                    if (this.value != null)
                    {
                        return this.value;
                    }

                    this.cancellationToken.ThrowIfCancellationRequested();
                    this.value = this.createValueFunc();
                    return this.value;
                }
            }

            public async Task<T> CreateAndWaitForBackgroundRefreshTaskAsync(
                Func<Task<T>> createRefreshTask,
                Action callbackOnRefreshFailure)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                // The original task is still being created. Just return the original task.
                Task<T> valueSnapshot = this.value;
                if (AsyncLazyWithRefreshTask<T>.IsTaskRunning(valueSnapshot))
                {
                    return await valueSnapshot;
                }

                // Use a local reference to avoid it being updated between the check and the await
                Task<T> refresh = this.refreshInProgress;
                if (AsyncLazyWithRefreshTask<T>.IsTaskRunning(refresh))
                {
                    return await refresh;
                }

                bool createdTask = false;
                lock (this.valueLock)
                {
                    if (AsyncLazyWithRefreshTask<T>.IsTaskRunning(this.refreshInProgress))
                    {
                        refresh = this.refreshInProgress;
                    }
                    else
                    {
                        createdTask = true;
                        this.refreshInProgress = createRefreshTask();
                        refresh = this.refreshInProgress;
                    }
                }

                // Await outside the lock to prevent lock contention
                if (!createdTask)
                {
                    return await refresh;
                }

                // It's possible multiple callers entered the method at the same time. The lock above ensures
                // only a single one will create the refresh task. If this caller created the task await for the
                // result and update the value.
                try
                {
                    T itemResult = await refresh;
                    lock (this)
                    {
                        this.value = Task.FromResult(itemResult);
                    }

                    return itemResult;
                }
                catch (Exception e)
                {
                    // faulted with exception
                    DefaultTrace.TraceError(
                        "AsyncLazyWithRefreshTask Failed with: {0}",
                        e.ToString());

                    callbackOnRefreshFailure();
                    throw;
                }
            }

            private static bool IsTaskRunning(Task t)
            {
                if (t == null)
                {
                    return false;
                }

                return !t.IsCompleted;
            }
        }

        private void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource.Dispose();
                }
                catch (ObjectDisposedException exception)
                {
                    // Need to access the exception to avoid unobserved exception
                    DefaultTrace.TraceInformation($"AsyncCacheNonBlocking was already disposed: {0}", exception);
                }

                this.isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
        }
    }
}