//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using Microsoft.Extensions.Caching.Distributed;

    /// <summary>
    /// Distributed (L2) cache configuration for the DEK properties cache. A non-null
    /// instance of this type on <see cref="DekCacheOptions.DistributedCache"/> enables L2;
    /// <see langword="null"/> disables it.
    /// </summary>
    /// <remarks>
    /// Wrapped (encrypted) DEK properties are written to <see cref="Cache"/>. Raw (unwrapped)
    /// DEK material is intentionally never written here. When configuring <see cref="Cache"/>,
    /// ensure the underlying cache infrastructure uses encryption in transit (TLS) and
    /// encryption at rest.
    /// </remarks>
    public sealed class DistributedCacheOptions
    {
        /// <summary>
        /// Gets or sets the <see cref="IDistributedCache"/> implementation. Required.
        /// </summary>
        public IDistributedCache Cache { get; set; }

        /// <summary>
        /// Gets or sets the prefix for distributed-cache keys. Required, must be unique per
        /// DEK container scope (for example derived from account/database/container identifiers)
        /// so that multiple providers sharing one cache instance cannot collide on identical
        /// DEK ids.
        /// </summary>
        public string KeyPrefix { get; set; }

        /// <summary>
        /// Gets or sets the optional absolute lifetime for entries written to <see cref="Cache"/>.
        /// Must be strictly greater than <see cref="DekCacheOptions.DekPropertiesTimeToLive"/>
        /// when both are specified, so that L2 entries can outlive a peer's L1 expiry. Defaults
        /// to twice <see cref="DekCacheOptions.DekPropertiesTimeToLive"/> when <see langword="null"/>.
        /// </summary>
        public TimeSpan? EntryLifetime { get; set; }
    }
}
