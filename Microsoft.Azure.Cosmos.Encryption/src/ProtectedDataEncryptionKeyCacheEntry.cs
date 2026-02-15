//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Represents a cached <see cref="ProtectedDataEncryptionKey"/> with a creation timestamp
    /// for TTL-based expiration in the SDK-side shadow cache.
    ///
    /// The shadow cache enables a fast-path lookup outside the global semaphore so that
    /// cache hits bypass the semaphore entirely (double-checked locking pattern).
    ///
    /// Expiration is checked against <see cref="ProtectedDataEncryptionKey.TimeToLive"/>
    /// at read time, so ratchet-downs from other <see cref="EncryptionCosmosClient"/>
    /// instances are respected immediately.
    /// </summary>
    internal readonly struct ProtectedDataEncryptionKeyCacheEntry
    {
        public ProtectedDataEncryptionKeyCacheEntry(ProtectedDataEncryptionKey protectedDataEncryptionKey)
        {
            this.ProtectedDataEncryptionKey = protectedDataEncryptionKey ?? throw new ArgumentNullException(nameof(protectedDataEncryptionKey));
            this.CreatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the cached <see cref="ProtectedDataEncryptionKey"/> instance.
        /// The object remains valid even after MDE's internal cache evicts it —
        /// the unwrapped key bytes live in the object, not in the cache.
        /// </summary>
        public ProtectedDataEncryptionKey ProtectedDataEncryptionKey { get; }

        /// <summary>
        /// Gets the UTC timestamp when this entry was created.
        /// Used with <see cref="ProtectedDataEncryptionKey.TimeToLive"/> to determine expiration.
        /// </summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>
        /// Gets a value indicating whether this cache entry has expired, based on the current
        /// <see cref="ProtectedDataEncryptionKey.TimeToLive"/> static value.
        /// Reading the live static TTL ensures that ratchet-downs
        /// (e.g., a second <see cref="EncryptionCosmosClient"/> created with a shorter TTL)
        /// take effect immediately for all cached entries.
        /// </summary>
        public bool IsExpired => (DateTime.UtcNow - this.CreatedAtUtc) >= ProtectedDataEncryptionKey.TimeToLive;
    }
}
