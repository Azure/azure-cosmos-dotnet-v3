//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// DekCache configurable options.
    /// </summary>
    public readonly struct DekCacheOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DekCacheOptions"/> struct.
        /// </summary>
        /// <param name="dekPropertiesTimeToLive"></param>
        /// <param name="cleanupIterationDelayInSeconds"></param>
        /// <param name="cleanupBufferTimeAfterExpiry"></param>
        public DekCacheOptions(
            TimeSpan? dekPropertiesTimeToLive = null,
            TimeSpan? cleanupIterationDelayInSeconds = null,
            TimeSpan? cleanupBufferTimeAfterExpiry = null)
        {
            this.DekPropertiesTimeToLive = dekPropertiesTimeToLive;
            this.CleanupIterationDelayInSeconds = cleanupIterationDelayInSeconds;
            this.CleanupBufferTimeAfterExpiry = cleanupBufferTimeAfterExpiry;
        }

        /// <summary>
        /// Time to live for DEK properties before having to refresh.
        /// </summary>
        internal TimeSpan? DekPropertiesTimeToLive { get; }

        /// <summary>
        /// Iteration delay for job cleaning up expired raw DEK from cache.
        /// </summary>
        internal TimeSpan? CleanupIterationDelayInSeconds { get; }

        /// <summary>
        /// Additional buffer time before cleaning up raw DEK.
        /// </summary>
        internal TimeSpan? CleanupBufferTimeAfterExpiry { get; }
    }
}
