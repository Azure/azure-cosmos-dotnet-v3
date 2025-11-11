//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using Microsoft.IO;

    /// <summary>
    /// Provides pooled memory streams via a shared RecyclableMemoryStreamManager instance for memory-efficient stream operations.
    /// </summary>
    internal static class MemoryStreamPool
    {
        /// <summary>
        /// Shared instance of the RecyclableMemoryStreamManager with default settings.
        /// </summary>
        private static readonly RecyclableMemoryStreamManager Instance = new ();

        /// <summary>
        /// Gets a new recyclable memory stream from the pool.
        /// </summary>
        /// <returns>A new recyclable memory stream from the pool.</returns>
        public static MemoryStream GetStream()
        {
            return MemoryStreamPool.Instance.GetStream();
        }

        /// <summary>
        /// Gets a new recyclable memory stream from the pool with a tag for tracking purposes.
        /// </summary>
        /// <param name="tag">A tag to identify the stream for tracking and diagnostics.</param>
        /// <returns>A new recyclable memory stream from the pool.</returns>
        public static MemoryStream GetStream(string tag)
        {
            return MemoryStreamPool.Instance.GetStream(tag);
        }

        /// <summary>
        /// Gets a new recyclable memory stream from the pool with an initial capacity.
        /// </summary>
        /// <param name="tag">A tag to identify the stream for tracking and diagnostics.</param>
        /// <param name="requiredSize">The initial capacity of the stream.</param>
        /// <returns>A new recyclable memory stream from the pool with the specified capacity.</returns>
        public static MemoryStream GetStream(string tag, int requiredSize)
        {
            return MemoryStreamPool.Instance.GetStream(tag, requiredSize);
        }
    }
}
