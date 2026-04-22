//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
internal sealed class SystemTextJsonStreamAdapter : IMdeJsonProcessorAdapter
{
    private readonly StreamProcessor streamProcessor;

    public SystemTextJsonStreamAdapter(StreamProcessor streamProcessor)
    {
        this.streamProcessor = streamProcessor;
    }

    public async Task<Stream> EncryptAsync(Stream input, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken)
    {
        PooledMemoryStream ms = new ();
        try
        {
            await this.streamProcessor.EncryptStreamAsync(input, ms, encryptor, options, cancellationToken);
            return ms;  // Ownership transfers successfully
        }
        catch
        {
            // CRITICAL: Dispose PooledMemoryStream on exception to prevent memory leak
            await ms.DisposeAsync();
            throw;  // Rethrow to preserve original exception
        }
    }

    public Task EncryptAsync(Stream input, Stream output, Encryptor encryptor, EncryptionOptions options, JsonProcessor jsonProcessor, CancellationToken cancellationToken)
    {
        if (jsonProcessor != JsonProcessor.Stream)
        {
            throw new NotSupportedException("This overload is only supported for Stream JsonProcessor");
        }

        return this.streamProcessor.EncryptStreamAsync(input, output, encryptor, options, cancellationToken);
    }

    public async Task<(Stream, DecryptionContext)> DecryptAsync(Stream input, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        EncryptionProperties properties = await ReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
        if (properties == null)
        {
            return (input, null);
        }

        // Write decrypt output through a pooled IBufferWriter and expose it as a
        // read-only Stream. This avoids the double-buffering that
        // Utf8JsonWriter(Stream) performs on .NET 8 (it eagerly creates a GC-heap-
        // backed ArrayBufferWriter<byte> that doubles from 256 bytes, producing
        // hundreds of KB of garbage per operation for medium documents).
        RentArrayBufferWriter bufferWriter = new (PooledStreamConfiguration.Current.StreamInitialCapacity);
        try
        {
            DecryptionContext context = await this.streamProcessor.DecryptStreamAsync(input, bufferWriter, encryptor, properties, diagnosticsContext, cancellationToken);
            if (context == null)
            {
                bufferWriter.Dispose();
                return (input, null);
            }

            // ReadOnlyBufferWriterStream takes ownership of bufferWriter; disposal of
            // the returned Stream (handled by the caller, e.g. ResponseMessage.Dispose)
            // returns the rented buffer to the pool.
            return (new ReadOnlyBufferWriterStream(bufferWriter), context);
        }
        catch
        {
            // CRITICAL: Dispose bufferWriter on exception to prevent memory leak
            bufferWriter.Dispose();
            throw;
        }
    }

    public async Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        EncryptionProperties properties = await ReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
        if (properties == null)
        {
            if (input.CanSeek)
            {
                input.Position = 0;
            }

            return null;
        }

        DecryptionContext context = await this.streamProcessor.DecryptStreamAsync(input, output, encryptor, properties, diagnosticsContext, cancellationToken);
        if (context == null)
        {
            if (input.CanSeek)
            {
                input.Position = 0;
            }

            return null;
        }

        await input.DisposeAsync();
        return context;
    }

    /// <summary>
    /// Reads encryption properties from the stream using a streaming <see cref="Utf8JsonReader"/>
    /// scan. Returns null if no encryption properties are found.
    /// Throws NotSupportedException if legacy encryption algorithm is detected.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="EncryptionPropertiesStreamReader"/> so that documents without
    /// unusually large root-level values stay within a small pooled buffer and never
    /// grow into the LOH bucket that a full-document <c>JsonSerializer.DeserializeAsync</c>
    /// forces on medium-size payloads.
    /// </remarks>
    private static async Task<EncryptionProperties> ReadMdeEncryptionPropertiesStreamingAsync(Stream input, CancellationToken cancellationToken)
    {
        EncryptionProperties encryptionProperties = await EncryptionPropertiesStreamReader.ReadAsync(input, PooledJsonSerializer.SerializerOptions, cancellationToken).ConfigureAwait(false);

        if (encryptionProperties == null)
        {
            return null;
        }

#pragma warning disable CS0618
        if (encryptionProperties.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
        {
            throw new NotSupportedException($"JsonProcessor.Stream is not supported for encryption algorithm '{encryptionProperties.EncryptionAlgorithm}'. Only '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' is supported with the Stream processor.");
        }
#pragma warning restore CS0618

        return encryptionProperties;
    }
}
#endif