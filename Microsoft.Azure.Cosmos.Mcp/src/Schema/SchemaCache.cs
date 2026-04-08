// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Schema
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// TTL-based cache for inferred container schemas.
    /// </summary>
    public class SchemaCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> cache = new();
        private readonly SchemaInferrer inferrer;
        private readonly TimeSpan cacheDuration;

        public SchemaCache(SchemaInferrer inferrer, TimeSpan cacheDuration)
        {
            this.inferrer = inferrer;
            this.cacheDuration = cacheDuration;
        }

        /// <summary>
        /// Gets the schema for a container, using the cache if available and not expired.
        /// </summary>
        public async Task<InferredSchema> GetSchemaAsync(
            CosmosContainer container,
            string databaseId,
            string containerId,
            int sampleSize,
            CancellationToken cancellationToken = default)
        {
            string key = $"{databaseId}/{containerId}";

            if (this.cache.TryGetValue(key, out CacheEntry? entry) && !entry.IsExpired)
            {
                return entry.Schema;
            }

            InferredSchema schema = await this.inferrer.InferSchemaAsync(container, sampleSize, cancellationToken);

            this.cache[key] = new CacheEntry(schema, this.cacheDuration);

            return schema;
        }

        /// <summary>
        /// Invalidates a cached schema for a specific container.
        /// </summary>
        public void Invalidate(string databaseId, string containerId)
        {
            string key = $"{databaseId}/{containerId}";
            this.cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears all cached schemas.
        /// </summary>
        public void Clear()
        {
            this.cache.Clear();
        }

        private class CacheEntry
        {
            public InferredSchema Schema { get; }
            public DateTimeOffset ExpiresAt { get; }
            public bool IsExpired => DateTimeOffset.UtcNow >= this.ExpiresAt;

            public CacheEntry(InferredSchema schema, TimeSpan duration)
            {
                this.Schema = schema;
                this.ExpiresAt = DateTimeOffset.UtcNow.Add(duration);
            }
        }
    }
}
