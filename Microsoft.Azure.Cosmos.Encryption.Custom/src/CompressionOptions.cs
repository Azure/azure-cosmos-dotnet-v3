// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO.Compression;

    /// <summary>
    /// Options for payload compression
    /// </summary>
    public class CompressionOptions
    {
        /// <summary>
        /// Supported compression algorithms
        /// </summary>
        /// <remarks>Compression is only supported with .NET8.0+.</remarks>
        public enum CompressionAlgorithm
        {
            /// <summary>
            /// No compression
            /// </summary>
            None = 0,
#if NET8_0_OR_GREATER

            /// <summary>
            /// Brotli compression
            /// </summary>
            Brotli = 1,
#endif
        }

        /// <summary>
        /// Gets or sets compression algorithm.
        /// </summary>
        public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.None;

        /// <summary>
        /// Gets or sets compression level.
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;

        /// <summary>
        /// Gets or sets minimal property size for compression.
        /// </summary>
        public int MinimalCompressedLength { get; set; } = 128;
    }
}