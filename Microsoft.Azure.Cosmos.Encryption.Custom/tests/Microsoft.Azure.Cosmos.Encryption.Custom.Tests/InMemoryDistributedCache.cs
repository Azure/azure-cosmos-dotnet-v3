//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;

    /// <summary>
    /// Simple in-memory implementation of IDistributedCache for testing.
    /// </summary>
    internal class InMemoryDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> cache = new ConcurrentDictionary<string, CacheEntry>();

        public byte[] Get(string key)
        {
            return this.GetAsync(key).GetAwaiter().GetResult();
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            if (this.cache.TryGetValue(key, out CacheEntry entry))
            {
                if (!entry.AbsoluteExpiration.HasValue || entry.AbsoluteExpiration.Value > DateTimeOffset.UtcNow)
                {
                    return Task.FromResult(entry.Value);
                }

                // Expired
                this.cache.TryRemove(key, out _);
            }

            return Task.FromResult<byte[]>(null);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            this.SetAsync(key, value, options).GetAwaiter().GetResult();
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            CacheEntry entry = new CacheEntry
            {
                Value = value,
                AbsoluteExpiration = options.AbsoluteExpiration,
            };

            this.cache[key] = entry;
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            this.cache.TryRemove(key, out _);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            this.Remove(key);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
            // No-op for this simple implementation
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public bool ContainsKey(string key)
        {
            return this.cache.ContainsKey(key);
        }

        private class CacheEntry
        {
            public byte[] Value { get; set; }

            public DateTimeOffset? AbsoluteExpiration { get; set; }
        }
    }
}
