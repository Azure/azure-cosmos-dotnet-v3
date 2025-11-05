//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// Configuration settings for pooled stream and buffer allocations used during
    /// System.Text.Json serialization/deserialization operations in encryption/decryption.
    /// </summary>
    internal static class PooledStreamConfiguration
    {
        /// <summary>
        /// Default initial capacity for PooledMemoryStream instances (4 KB).
        /// This size is optimized for typical encrypted document payloads.
        /// </summary>
        public const int DefaultStreamInitialCapacity = 4096;

        /// <summary>
        /// Default initial capacity for RentArrayBufferWriter instances (256 bytes).
        /// This is suitable for encryption metadata and smaller serialization operations.
        /// </summary>
        public const int DefaultBufferWriterInitialCapacity = 256;

        /// <summary>
        /// Initial buffer size for streaming JSON operations (16 KB).
        /// Used by StreamProcessor for reading chunks during encrypt/decrypt operations.
        /// </summary>
        public const int DefaultStreamProcessorBufferSize = 16384;

        /// <summary>
        /// Maximum array length to prevent overflow in buffer allocations.
        /// </summary>
        public const int MaxArrayLength = 0X7FFFFFC7; // From Array.MaxLength

        /// <summary>
        /// Gets or sets the initial capacity for PooledMemoryStream instances.
        /// Default is 4096 bytes (4 KB).
        /// </summary>
        /// <remarks>
        /// Adjust this value based on your typical encrypted document sizes:
        /// - Smaller documents: 2048 bytes (2 KB)
        /// - Medium documents: 4096 bytes (4 KB) - Default
        /// - Larger documents: 8192 bytes (8 KB) or higher
        /// </remarks>
        public static int StreamInitialCapacity { get; set; } = DefaultStreamInitialCapacity;

        /// <summary>
        /// Gets or sets the initial capacity for RentArrayBufferWriter instances.
        /// Default is 256 bytes.
        /// </summary>
        /// <remarks>
        /// This is used for intermediate buffering during encryption operations.
        /// Typically does not need to be adjusted unless encrypting very large individual properties.
        /// </remarks>
        public static int BufferWriterInitialCapacity { get; set; } = DefaultBufferWriterInitialCapacity;

        /// <summary>
        /// Gets or sets a value indicating whether to clear arrays when returning them to the pool.
        /// Default is true for security reasons.
        /// </summary>
        /// <remarks>
        /// Setting this to false can improve performance but may leave sensitive data in memory.
        /// Only disable if you have other security measures in place.
        /// </remarks>
        public static bool ClearArraysOnReturn { get; set; } = true;
    }
}
#endif
