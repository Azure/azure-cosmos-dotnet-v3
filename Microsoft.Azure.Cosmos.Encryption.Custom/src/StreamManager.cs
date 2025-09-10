// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for pooling streams
    /// </summary>
    public abstract class StreamManager
    {
        /// <summary>
        /// Create stream
        /// </summary>
        /// <param name="hintSize">Desired minimal size of stream.</param>
        /// <returns>Instance of stream.</returns>
        public abstract Stream CreateStream(int hintSize = 0);

        /// <summary>
        /// Dispose of used Stream (return to pool)
        /// </summary>
        /// <param name="stream">Stream to dispose.</param>
        /// <returns>ValueTask.CompletedTask</returns>
        public abstract ValueTask ReturnStreamAsync(Stream stream);
    }
}
