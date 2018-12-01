//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cache which supports asynchronous value initialization.
    /// It ensures that for given key only single inintialization funtion is running at any point in time.
    /// </summary>
    /// <typeparam name="TKey">Type of keys.</typeparam>
    /// <typeparam name="TValue">Type of values.</typeparam>
    internal sealed class AsyncCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, AsyncLazy<TValue>> values;

        private readonly IEqualityComparer<TValue> valueEqualityComparer;

        public AsyncCache(IEqualityComparer<TValue> valueEqualityComparer, IEqualityComparer<TKey> keyEqualityComparer = null)
        {
            this.values = new ConcurrentDictionary<TKey, AsyncLazy<TValue>>(keyEqualityComparer ?? EqualityComparer<TKey>.Default);
            this.valueEqualityComparer = valueEqualityComparer;
        }

        public AsyncCache() : this(EqualityComparer<TValue>.Default)
        {
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return this.values.Keys;
            }
        }

        public void Set(TKey key, TValue value)
        {
            this.values[key] = new AsyncLazy<TValue>(() => value, CancellationToken.None);
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
        /// If previous initialization function is successfully completed - value returned by it will be returned unless
        /// it is equal to <paramref name="obsoleteValue"/>, in which case new initialization function will be started.
        /// </para>
        /// <para>
        /// If previous initialization function failed - new one will be launched.
        /// </para>
        /// </summary>
        /// <param name="key">Key for which to get a value.</param>
        /// <param name="obsoleteValue">Value which is obsolete and needs to be refreshed.</param>
        /// <param name="singleValueInitFunc">Initialization function.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cached value or value returned by initialization function.</returns>
        public async Task<TValue> GetAsync(
           TKey key,
           TValue obsoleteValue,
           Func<Task<TValue>> singleValueInitFunc,
           CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AsyncLazy<TValue> initialLazyValue;
            if (this.values.TryGetValue(key, out initialLazyValue) && !initialLazyValue.IsFaultedOrCancelled)
            {
                try
                {
                    if (!initialLazyValue.IsCompleted || !this.valueEqualityComparer.Equals(await initialLazyValue.Value, obsoleteValue))
                    {
                        return await initialLazyValue.Value;
                    }
                }
                // another thread failed and caused the cached task to fail as well.
                // re-enqueue our task.
                catch
                {
                }
            }

            AsyncLazy<TValue> newLazyValue = new AsyncLazy<TValue>(singleValueInitFunc, cancellationToken);

            // Update the new task in the cache, 
            AsyncLazy<TValue> actualValue = this.values.AddOrUpdate(
                key,
                newLazyValue,
                (existingKey, existingValue) => object.ReferenceEquals(existingValue, initialLazyValue) ? newLazyValue : existingValue);

            return await actualValue.Value;
        }

        public void Remove(TKey key)
        {
            AsyncLazy<TValue> initialLazyValue;
            this.values.TryRemove(key, out initialLazyValue);
        }

        /// <summary>
        /// Remove value from cache and return it if present
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Value if present, default value if not present</returns>
        public async Task<TValue> RemoveAsync(TKey key)
        {
            AsyncLazy<TValue> initialLazyValue;
            if (this.values.TryRemove(key, out initialLazyValue) && !initialLazyValue.IsFaultedOrCancelled)
            {
                return await initialLazyValue.Value;
            }

            return default(TValue);
        }

        public void Clear()
        {
            this.values.Clear();
        }

        /// <summary>
        /// Forces refresh of the cached item if it is not being refreshed at the moment.
        /// </summary>
        public void Refresh(
            TKey key,
            Func<Task<TValue>> singleValueInitFunc,
            CancellationToken cancellationToken)
        {
            AsyncLazy<TValue> initialLazyValue;
            if (this.values.TryGetValue(key, out initialLazyValue) && initialLazyValue.IsCompleted)
            {
                AsyncLazy<TValue> newLazyValue = new AsyncLazy<TValue>(singleValueInitFunc, cancellationToken);

                // Update the new task in the cache, 
                AsyncLazy<TValue> actualValue = this.values.AddOrUpdate(
                    key,
                    newLazyValue,
                    (existingKey, existingValue) => object.ReferenceEquals(existingValue, initialLazyValue) ? newLazyValue : existingValue);
            }
        }
    }
}
