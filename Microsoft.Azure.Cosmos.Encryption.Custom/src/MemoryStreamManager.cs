// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.IO;

    /// <summary>
    /// Memory Stream manager
    /// </summary>
    /// <remarks>Placeholder</remarks>
    internal class MemoryStreamManager : StreamManager
    {
        private readonly RecyclableMemoryStreamManager streamManager = new ();

        /// <summary>
        /// Create stream
        /// </summary>
        /// <param name="hintSize">Desired minimal capacity of stream.</param>
        /// <returns>Instance of stream.</returns>
        public override Stream CreateStream(int hintSize = 0)
        {
            return new RecyclableMemoryStream(this.streamManager, null, hintSize);
        }

        /// <summary>
        /// Dispose of used Stream (return to pool)
        /// </summary>
        /// <param name="stream">Stream to dispose.</param>
        /// <returns>ValueTask.CompletedTask</returns>
        public async override ValueTask ReturnStreamAsync(Stream stream)
        {
            await stream.DisposeAsync();
        }
    }
}
#endif