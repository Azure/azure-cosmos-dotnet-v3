//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

internal sealed class NewtonsoftAdapter : IMdeJsonProcessorAdapter
{
    private readonly MdeJObjectEncryptionProcessor jObjectProcessor;

    public NewtonsoftAdapter(MdeJObjectEncryptionProcessor jObjectProcessor)
    {
        this.jObjectProcessor = jObjectProcessor;
    }

    public Task<Stream> EncryptAsync(
        Stream input,
        Encryptor encryptor,
        EncryptionOptions options,
        CancellationToken cancellationToken)
    {
        return this.EncryptAsync(
            input,
            encryptor,
            options,
            cancellationToken,
            replacePlaintextEncryptionMetadata: false);
    }

    public Task<Stream> EncryptAsync(
        Stream input,
        Encryptor encryptor,
        EncryptionOptions options,
        CancellationToken cancellationToken,
        bool replacePlaintextEncryptionMetadata)
    {
        return this.jObjectProcessor.EncryptAsync(
            input,
            encryptor,
            options,
            cancellationToken,
            replacePlaintextEncryptionMetadata);
    }

    public Task EncryptAsync(Stream input, Stream output, Encryptor encryptor, EncryptionOptions options, JsonProcessor jsonProcessor, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This overload is only supported for Stream JsonProcessor");
    }

    public async Task<(Stream, DecryptionContext)> DecryptAsync(Stream input, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        (EncryptionMetadataDisposition status, JObject itemJObj, EncryptionProperties encryptionProperties) = this.InspectForMde(input);
        switch (status)
        {
            case EncryptionMetadataDisposition.Mde:
                {
                    DecryptionContext context = await this.jObjectProcessor.DecryptObjectAsync(itemJObj, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
                    await input.DisposeCompatAsync();

                    MemoryStream direct = new (capacity: 1024);
                    EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, direct);
                    direct.Position = 0; // Reset position for caller to read from beginning
                    return (direct, context);
                }

            case EncryptionMetadataDisposition.None:
            case EncryptionMetadataDisposition.Plaintext:
            case EncryptionMetadataDisposition.Legacy:
            case EncryptionMetadataDisposition.Unsupported:
            default:
                return (input, null);
        }
    }

    public async Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        (EncryptionMetadataDisposition status, JObject itemJObj, EncryptionProperties encryptionProperties) = this.InspectForMde(input);
        switch (status)
        {
            case EncryptionMetadataDisposition.Mde:
                {
                    DecryptionContext context = await this.jObjectProcessor.DecryptObjectAsync(itemJObj, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
                    EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, output);
                    output.Position = 0; // Reset position for caller to read from beginning
                    await input.DisposeCompatAsync();
                    return context;
                }

            case EncryptionMetadataDisposition.None:
            case EncryptionMetadataDisposition.Plaintext:
            case EncryptionMetadataDisposition.Legacy:
            case EncryptionMetadataDisposition.Unsupported:
            default:
                return null;
        }
    }

    private (EncryptionMetadataDisposition status, JObject itemJObj, EncryptionProperties encryptionProperties) InspectForMde(Stream input)
    {
        JObject itemJObj = NewtonsoftJsonObjectReader.Read(input);
        JToken encryptionMetadata = itemJObj[Constants.EncryptedInfo];
        EncryptionMetadataDisposition disposition = EncryptionMetadataClassifier.Classify(encryptionMetadata);
        EncryptionMetadataClassifier.ThrowIfInvalid(disposition);
        if (disposition == EncryptionMetadataDisposition.Unsupported)
        {
            throw new NotSupportedException("The document uses an unsupported encryption algorithm.");
        }

        if (disposition != EncryptionMetadataDisposition.Mde)
        {
            return (disposition, null, null);
        }

        return (
            disposition,
            itemJObj,
            ((JObject)encryptionMetadata).ToObject<EncryptionProperties>());
    }
}