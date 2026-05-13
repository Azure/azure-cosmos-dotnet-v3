//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using Microsoft.Extensions.Caching.Distributed;

    /// <summary>
    /// Options for configuring the <see cref="CosmosDataEncryptionKeyProvider"/>'s data
    /// encryption key (DEK) cache, including the optional distributed cache layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefer this options bag over additional positional constructor parameters so that future
    /// cache knobs (additional distributed-cache settings, telemetry hooks, etc.) can be added
    /// as properties without producing yet another constructor overload per provider variant.
    /// </para>
    /// <para>
    /// <strong>Scope:</strong> these options apply only to caching of
    /// <see cref="DataEncryptionKeyProperties"/>. Raw (unwrapped) DEK material remains
    /// process-local for security and is intentionally never written to
    /// <see cref="DistributedCache"/>.
    /// </para>
    /// <para>
    /// When configuring <see cref="DistributedCache"/>, ensure the cache infrastructure uses
    /// encryption in transit (TLS) and encryption at rest. The serialised payload contains
    /// wrapped (encrypted) DEK metadata.
    /// </para>
    /// </remarks>
    public sealed class DekCacheOptions
    {
        /// <summary>
        /// Gets or sets the time to live for cached <see cref="DataEncryptionKeyProperties"/> in
        /// the in-memory (L1) cache before refresh. Defaults to the SDK's internal default when
        /// <see langword="null"/>.
        /// </summary>
        public TimeSpan? DekPropertiesTimeToLive { get; set; }

        /// <summary>
        /// Gets or sets the optional distributed (L2) cache implementation enabling
        /// cross-process / cross-instance caching of DEK properties. Leave <see langword="null"/>
        /// to use only the in-memory cache.
        /// </summary>
        public IDistributedCache DistributedCache { get; set; }

        /// <summary>
        /// Gets or sets the optional time window before expiry to trigger a non-blocking
        /// proactive background refresh. For example, <c>TimeSpan.FromMinutes(10)</c> refreshes
        /// a DEK 10 minutes before its in-memory entry expires. Must be non-negative and
        /// strictly less than <see cref="DekPropertiesTimeToLive"/> when both are specified.
        /// </summary>
        public TimeSpan? RefreshBeforeExpiry { get; set; }

        /// <summary>
        /// Gets or sets the prefix for distributed cache keys. Required when
        /// <see cref="DistributedCache"/> is non-<see langword="null"/>: must be unique per
        /// DEK container scope (for example, derived from account/database/container identifiers)
        /// so that multiple providers sharing the same cache instance do not collide on
        /// identical DEK ids. Ignored when <see cref="DistributedCache"/> is
        /// <see langword="null"/>.
        /// </summary>
        public string DistributedCacheKeyPrefix { get; set; }

        /// <summary>
        /// Gets or sets the optional absolute lifetime for entries written to
        /// <see cref="DistributedCache"/>. Must be strictly greater than
        /// <see cref="DekPropertiesTimeToLive"/> so that L2 entries can outlive a peer's L1
        /// expiry and rescue requests when the source of truth is momentarily unavailable.
        /// Defaults to twice <see cref="DekPropertiesTimeToLive"/> when <see langword="null"/>.
        /// </summary>
        public TimeSpan? DistributedCacheEntryLifetime { get; set; }
    }
}
