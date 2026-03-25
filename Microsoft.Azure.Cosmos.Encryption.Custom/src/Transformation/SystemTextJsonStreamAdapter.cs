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
        EncryptionProperties properties = await this.ReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
        if (properties == null)
        {
            return (input, null);
        }

        PooledMemoryStream ms = new ();
        try
        {
            DecryptionContext context = await this.streamProcessor.DecryptStreamAsync(input, ms, encryptor, properties, diagnosticsContext, cancellationToken);
            if (context == null)
            {
                await ms.DisposeAsync();
                return (input, null);
            }

            return (ms, context);  // Ownership transfers successfully
        }
        catch
        {
            // CRITICAL: Dispose PooledMemoryStream on exception to prevent memory leak
            await ms.DisposeAsync();
            throw;  // Rethrow to preserve original exception
        }
    }

    public async Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        EncryptionProperties properties = await this.ReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
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
    /// Reads encryption properties from the stream using System.Text.Json streaming API.
    /// Returns null if no encryption properties are found.
    /// Throws NotSupportedException if legacy encryption algorithm is detected.
    /// </summary>
    private async Task<EncryptionProperties> ReadMdeEncryptionPropertiesStreamingAsync(Stream input, CancellationToken cancellationToken)
    {
        input.Position = 0;
        EncryptionPropertiesWrapper properties = await PooledJsonSerializer.DeserializeFromStreamAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
        input.Position = 0;
        if (properties?.EncryptionProperties == null)
        {
            return null;
        }

#pragma warning disable CS0618
        if (properties.EncryptionProperties.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
        {
            throw new NotSupportedException($"JsonProcessor.Stream is not supported for encryption algorithm '{properties.EncryptionProperties.EncryptionAlgorithm}'. Only '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' is supported with the Stream processor.");
        }
#pragma warning restore CS0618

        return properties.EncryptionProperties;
    }
}
#endif