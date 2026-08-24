//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Options for configuring the <see cref="CosmosDataEncryptionKeyProvider"/>'s DEK cache.
    /// </summary>
    /// <remarks>
    /// Distributed (L2) cache is enabled by setting <see cref="DistributedCache"/> to a non-null
    /// <see cref="DistributedCacheOptions"/>. Leaving it <see langword="null"/> uses only the
    /// in-memory (L1) cache.
    /// </remarks>
    public sealed class DekCacheOptions
    {
        /// <summary>
        /// Gets or sets the time to live for cached DEK properties in the in-memory (L1) cache
        /// before refresh. Defaults to the SDK internal default when <see langword="null"/>.
        /// </summary>
        public TimeSpan? DekPropertiesTimeToLive { get; set; }

        /// <summary>
        /// Gets or sets the optional time window before expiry that triggers a non-blocking
        /// proactive background refresh. Must be non-negative and strictly less than
        /// <see cref="DekPropertiesTimeToLive"/> when both are specified.
        /// </summary>
        public TimeSpan? RefreshBeforeExpiry { get; set; }

        /// <summary>
        /// Gets or sets the optional distributed (L2) cache configuration. A non-null value
        /// enables L2; <see langword="null"/> disables it.
        /// </summary>
        public DistributedCacheOptions DistributedCache { get; set; }
    }
}
