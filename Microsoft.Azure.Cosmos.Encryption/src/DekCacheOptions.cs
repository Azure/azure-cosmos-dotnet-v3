//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Struct to specify configurable options for Data Encryption Key cache (DekCache).
    /// </summary>
    public readonly struct DekCacheOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DekCacheOptions"/> struct.
        /// </summary>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        /// <param name="cleanupInterval">Time interval between successive runs of cleanup task.</param>
        /// <param name="cleanupBufferTimeAfterExpiry">Additional buffer time before cleaning up raw DEK.</param>
        public DekCacheOptions(
            TimeSpan? dekPropertiesTimeToLive = null,
            TimeSpan? cleanupInterval = null,
            TimeSpan? cleanupBufferTimeAfterExpiry = null)
        {
            this.DekPropertiesTimeToLive = dekPropertiesTimeToLive;
            this.CleanupInterval = cleanupInterval;
            this.CleanupBufferTimeAfterExpiry = cleanupBufferTimeAfterExpiry;
        }

        /// <summary>
        /// Time to live for DEK properties before having to refresh.
        /// </summary>
        /// <remarks>Set ServerPropertiesExpiryUtc (time to live before expiring) for <see cref="CachedDekProperties"/></remarks>
        public TimeSpan? DekPropertiesTimeToLive { get; }

        /// <summary>
        /// Time interval between successive runs of cleanup task.
        /// </summary>
        public TimeSpan? CleanupInterval { get; }

        /// <summary>
        /// Additional buffer time before cleaning up raw DEK.
        /// </summary>
        public TimeSpan? CleanupBufferTimeAfterExpiry { get; }
    }
}
