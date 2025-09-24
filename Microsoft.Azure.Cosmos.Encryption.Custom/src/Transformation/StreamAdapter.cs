//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
internal sealed class StreamAdapter : IMdeJsonProcessorAdapter
{
    private readonly StreamProcessor streamProcessor;

    public StreamAdapter(StreamProcessor streamProcessor)
    {
        this.streamProcessor = streamProcessor;
    }

    public async Task<Stream> EncryptAsync(Stream input, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken)
    {
        MemoryStream ms = new ();
        await this.streamProcessor.EncryptStreamAsync(input, ms, encryptor, options, cancellationToken);
        return ms;
    }

    public Task EncryptAsync(Stream input, Stream output, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken)
    {
        if (options.JsonProcessor != JsonProcessor.Stream)
        {
            throw new NotSupportedException("This overload is only supported for Stream JsonProcessor");
        }

        return this.streamProcessor.EncryptStreamAsync(input, output, encryptor, options, cancellationToken);
    }

    public async Task<(Stream, DecryptionContext)> DecryptAsync(Stream input, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        using (diagnosticsContext.CreateScope(EncryptionDiagnostics.ScopeDecryptStreamImplMde))
        {
            (bool hasMde, EncryptionProperties properties) = await this.TryReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
            if (!hasMde)
            {
                return (input, null);
            }

            MemoryStream ms = new ();
            DecryptionContext context = await this.streamProcessor.DecryptStreamAsync(input, ms, encryptor, properties, diagnosticsContext, cancellationToken);
            if (context == null)
            {
                return (input, null);
            }

            return (ms, context);
        }
    }

    public async Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        using (diagnosticsContext.CreateScope(EncryptionDiagnostics.ScopeDecryptStreamImplMde))
        {
            (bool hasMde, EncryptionProperties properties) = await this.TryReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
            if (!hasMde)
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
    }

    private async Task<(bool hasMde, EncryptionProperties properties)> TryReadMdeEncryptionPropertiesStreamingAsync(Stream input, CancellationToken cancellationToken)
    {
        input.Position = 0;
        EncryptionPropertiesWrapper properties = await JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
        input.Position = 0;
        if (properties?.EncryptionProperties == null)
        {
            return (false, null);
        }

#pragma warning disable CS0618
        if (properties.EncryptionProperties.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
        {
            return (false, null);
        }
#pragma warning restore CS0618

        return (true, properties.EncryptionProperties);
    }
}
#endif
#endif