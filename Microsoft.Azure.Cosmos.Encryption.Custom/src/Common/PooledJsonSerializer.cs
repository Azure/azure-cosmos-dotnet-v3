//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides optimized System.Text.Json serialization/deserialization using ArrayPool-backed buffers.
    /// </summary>
    /// <remarks>
    /// <para><strong>Thread Safety:</strong></para>
    /// <para>
    /// All methods are thread-safe and can be called concurrently from multiple threads. Each invocation
    /// creates independent PooledMemoryStream instances, and JsonSerializer.Serialize/Deserialize are
    /// thread-safe when using separate stream instances.
    /// </para>
    /// <para><strong>Disposal Requirements:</strong></para>
    /// <para>
    /// Methods returning PooledMemoryStream transfer ownership to the caller. The caller MUST dispose
    /// the returned stream to prevent memory leaks. Methods accepting Stream parameters do NOT dispose
    /// the input stream - disposal remains the caller's responsibility.
    /// </para>
    /// <para><strong>Performance Considerations:</strong></para>
    /// <para>
    /// Uses ArrayPool-backed buffers configured via PooledStreamConfiguration. Reduces GC pressure
    /// compared to standard MemoryStream. Deserialization reads directly from input streams without
    /// intermediate buffering when possible.
    /// </para>
    /// </remarks>
    internal static class PooledJsonSerializer
    {
        private static readonly JsonSerializerOptions DefaultOptions = new ()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Serializes an object to a PooledMemoryStream using ArrayPool-backed buffers.
        /// </summary>
        public static PooledMemoryStream SerializeToPooledStream<T>(T value, JsonSerializerOptions options = null)
        {
            PooledMemoryStream stream = new (
                capacity: PooledStreamConfiguration.StreamInitialCapacity,
                clearOnReturn: PooledStreamConfiguration.ClearArraysOnReturn);

            try
            {
                using (Utf8JsonWriter writer = new (stream, new JsonWriterOptions { SkipValidation = false }))
                {
                    JsonSerializer.Serialize(writer, value, options ?? DefaultOptions);
                    writer.Flush();
                }

                stream.Position = 0;
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Serializes an object directly to a stream using Utf8JsonWriter with minimal allocations.
        /// </summary>
        public static void SerializeToStream<T>(Stream stream, T value, JsonSerializerOptions options = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (Utf8JsonWriter writer = new (stream, new JsonWriterOptions { SkipValidation = false }))
            {
                JsonSerializer.Serialize(writer, value, options ?? DefaultOptions);
                writer.Flush();
            }
        }

        /// <summary>
        /// Serializes an object directly to a stream asynchronously using Utf8JsonWriter with minimal allocations.
        /// </summary>
        public static async Task SerializeToStreamAsync<T>(Stream stream, T value, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            await using (Utf8JsonWriter writer = new (stream, new JsonWriterOptions { SkipValidation = false }))
            {
                JsonSerializer.Serialize(writer, value, options ?? DefaultOptions);
                await writer.FlushAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Serializes an object to a RentArrayBufferWriter using ArrayPool-backed buffers.
        /// Returns the buffer writer which must be disposed by the caller.
        /// </summary>
        public static RentArrayBufferWriter SerializeToBufferWriter<T>(T value, JsonSerializerOptions options = null)
        {
            RentArrayBufferWriter bufferWriter = new (PooledStreamConfiguration.BufferWriterInitialCapacity);

            try
            {
                using (Utf8JsonWriter writer = new (bufferWriter, new JsonWriterOptions { SkipValidation = false }))
                {
                    JsonSerializer.Serialize(writer, value, options ?? DefaultOptions);
                    writer.Flush();
                }

                return bufferWriter;
            }
            catch
            {
                bufferWriter.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Deserializes from a stream using System.Text.Json with minimal allocations.
        /// </summary>
        public static T DeserializeFromStream<T>(Stream stream, JsonSerializerOptions options = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return JsonSerializer.Deserialize<T>(stream, options ?? DefaultOptions);
        }

        /// <summary>
        /// Deserializes from a stream asynchronously using System.Text.Json with minimal allocations.
        /// </summary>
        public static async ValueTask<T> DeserializeFromStreamAsync<T>(Stream stream, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return await JsonSerializer.DeserializeAsync<T>(stream, options ?? DefaultOptions, cancellationToken);
        }

        /// <summary>
        /// Deserializes from a byte span using Utf8JsonReader with zero allocations.
        /// </summary>
        public static T DeserializeFromSpan<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options = null)
        {
            Utf8JsonReader reader = new (utf8Json);
            return JsonSerializer.Deserialize<T>(ref reader, options ?? DefaultOptions);
        }

        /// <summary>
        /// Serializes an object to a pooled byte array.
        /// Returns the rented array and the actual length written.
        /// The array must be returned to ArrayPool.Shared by the caller.
        /// </summary>
        public static (byte[] Buffer, int Length) SerializeToPooledArray<T>(T value, JsonSerializerOptions options = null)
        {
            using RentArrayBufferWriter bufferWriter = new ();
            using (Utf8JsonWriter writer = new (bufferWriter, new JsonWriterOptions { SkipValidation = false }))
            {
                JsonSerializer.Serialize(writer, value, options ?? DefaultOptions);
                writer.Flush();
            }

            (byte[] buffer, int length) = bufferWriter.WrittenBuffer;

            // Copy to a separate rented buffer since bufferWriter will be disposed
            byte[] result = ArrayPool<byte>.Shared.Rent(length);
            Buffer.BlockCopy(buffer, 0, result, 0, length);

            return (result, length);
        }
    }
}
#endif
