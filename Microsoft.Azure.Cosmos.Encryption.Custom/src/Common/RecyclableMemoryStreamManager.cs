//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using Microsoft.IO;

    /// <summary>
    /// Provides a singleton instance of RecyclableMemoryStreamManager for memory-efficient stream operations.
    /// </summary>
    internal static class RecyclableMemoryStreamManager
    {
        /// <summary>
        /// Shared instance of the RecyclableMemoryStreamManager with default settings.
        /// </summary>
        private static readonly Microsoft.IO.RecyclableMemoryStreamManager Instance = new ();

        /// <summary>
        /// Gets a new recyclable memory stream.
        /// </summary>
        /// <returns>A new recyclable memory stream from the pool.</returns>
        public static MemoryStream GetStream()
        {
            return RecyclableMemoryStreamManager.Instance.GetStream();
        }

        /// <summary>
        /// Gets a new recyclable memory stream with a tag for tracking purposes.
        /// </summary>
        /// <param name="tag">A tag to identify the stream for tracking and diagnostics.</param>
        /// <returns>A new recyclable memory stream from the pool.</returns>
        public static MemoryStream GetStream(string tag)
        {
            return RecyclableMemoryStreamManager.Instance.GetStream(tag);
        }

        /// <summary>
        /// Gets a new recyclable memory stream with an initial capacity.
        /// </summary>
        /// <param name="tag">A tag to identify the stream for tracking and diagnostics.</param>
        /// <param name="requiredSize">The initial capacity of the stream.</param>
        /// <returns>A new recyclable memory stream from the pool with the specified capacity.</returns>
        public static MemoryStream GetStream(string tag, int requiredSize)
        {
            return RecyclableMemoryStreamManager.Instance.GetStream(tag, requiredSize);
        }
    }
}
