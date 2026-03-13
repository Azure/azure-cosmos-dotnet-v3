// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace CachingKeyResolverSample
{
    using System;

    /// <summary>
    /// Configuration options for <see cref="CachingKeyResolver"/>.
    /// </summary>
    public sealed class CachingKeyResolverOptions
    {
        /// <summary>
        /// How long cached key encryption keys remain valid before they must be re-resolved
        /// from the inner resolver. Default: 2 hours.
        /// </summary>
        /// <remarks>
        /// This value must be shorter than your key rotation interval to ensure that
        /// rotated keys are picked up in a timely manner.
        /// </remarks>
        public TimeSpan KeyCacheTimeToLive { get; init; } = TimeSpan.FromHours(2);

        /// <summary>
        /// How long before a cache entry expires to proactively start a background refresh.
        /// Default: 5 minutes.
        /// </summary>
        /// <remarks>
        /// Setting this to a value larger than <see cref="KeyCacheTimeToLive"/> effectively
        /// disables proactive refresh.
        /// </remarks>
        public TimeSpan ProactiveRefreshThreshold { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How often the background timer fires to check for cache entries needing refresh.
        /// Default: 1 minute.
        /// </summary>
        public TimeSpan RefreshTimerInterval { get; init; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long cached <c>UnwrapKey</c> results (raw DEK bytes) are considered fresh.
        /// After this time, a live AKV call is attempted first, but the cached bytes are
        /// still used as a stale fallback if AKV is unavailable.
        /// Default: 24 hours — safe when key rotation is infrequent.
        /// </summary>
        /// <remarks>
        /// Set to <see cref="TimeSpan.Zero"/> to disable <c>UnwrapKey</c> caching entirely
        /// (the resolver will still cache the key handle from <c>Resolve</c>).
        /// </remarks>
        public TimeSpan UnwrapKeyCacheTimeToLive { get; init; } = TimeSpan.FromHours(24);
    }
}
