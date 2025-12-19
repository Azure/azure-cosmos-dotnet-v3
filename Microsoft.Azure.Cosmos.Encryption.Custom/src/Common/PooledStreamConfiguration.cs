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
    /// <remarks>
    /// <para><strong>Thread Safety:</strong></para>
    /// <para>
    /// This configuration object is immutable and thread-safe. Reading configuration values
    /// from the Current property is always safe from multiple threads. Updating configuration
    /// via SetConfiguration performs an atomic reference swap, ensuring readers always see a
    /// consistent configuration snapshot.
    /// </para>
    /// <para><strong>Configuration Timing:</strong></para>
    /// <para>
    /// Configure once at application startup using SetConfiguration before any encryption/decryption
    /// operations begin. While the configuration swap itself is atomic, changing values during runtime
    /// while operations are in progress may cause inconsistent behavior as some operations will use
    /// old values while others use new values.
    /// </para>
    /// <para><strong>Security Considerations:</strong></para>
    /// <para>
    /// Buffer arrays are always cleared before being returned to the pool to prevent sensitive
    /// plaintext data from remaining in memory. This security measure cannot be disabled.
    /// </para>
    /// </remarks>
    internal sealed class PooledStreamConfiguration
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
        /// Gets the default configuration instance with standard buffer sizes.
        /// </summary>
        public static PooledStreamConfiguration Default { get; } = new PooledStreamConfiguration();

        private static PooledStreamConfiguration current = Default;

        /// <summary>
        /// Gets the current active configuration. Thread-safe.
        /// </summary>
        public static PooledStreamConfiguration Current => current;

        /// <summary>
        /// Sets a new configuration atomically. Thread-safe.
        /// </summary>
        /// <param name="configuration">The new configuration to use. Cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when configuration is null.</exception>
        /// <remarks>
        /// This performs an atomic reference swap. All operations started after this call
        /// will use the new configuration. Operations already in progress will continue
        /// using their captured configuration values.
        /// </remarks>
        public static void SetConfiguration(PooledStreamConfiguration configuration)
        {
            current = configuration ?? throw new System.ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Gets the initial capacity for PooledMemoryStream instances.
        /// Default is 4096 bytes (4 KB).
        /// </summary>
        /// <remarks>
        /// Choose this value based on your typical encrypted document sizes:
        /// - Smaller documents: 2048 bytes (2 KB)
        /// - Medium documents: 4096 bytes (4 KB) - Default
        /// - Larger documents: 8192 bytes (8 KB) or higher
        /// </remarks>
        public int StreamInitialCapacity { get; init; } = DefaultStreamInitialCapacity;

        /// <summary>
        /// Gets the initial capacity for RentArrayBufferWriter instances.
        /// Default is 256 bytes.
        /// </summary>
        /// <remarks>
        /// This is used for intermediate buffering during encryption operations.
        /// Typically does not need to be adjusted unless encrypting very large individual properties.
        /// </remarks>
        public int BufferWriterInitialCapacity { get; init; } = DefaultBufferWriterInitialCapacity;

        /// <summary>
        /// Gets the initial buffer size for StreamProcessor operations.
        /// Default is 16384 bytes (16 KB).
        /// </summary>
        /// <remarks>
        /// Used for reading chunks during streaming encrypt/decrypt operations.
        /// Smaller values (e.g., 32-256 bytes) are useful for testing buffer resize logic.
        /// </remarks>
        public int StreamProcessorBufferSize { get; init; } = DefaultStreamProcessorBufferSize;
    }
}
#endif
