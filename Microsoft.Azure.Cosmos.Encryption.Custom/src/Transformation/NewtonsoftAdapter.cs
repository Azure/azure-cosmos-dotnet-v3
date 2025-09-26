//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

internal sealed class NewtonsoftAdapter : IMdeJsonProcessorAdapter
{
    private enum MdePropertyStatus
    {
        None,
        Mde,
        LegacyOther,
    }

    private readonly MdeJObjectEncryptionProcessor jObjectProcessor;

    public NewtonsoftAdapter(MdeJObjectEncryptionProcessor jObjectProcessor)
    {
        this.jObjectProcessor = jObjectProcessor;
    }

    public Task<Stream> EncryptAsync(Stream input, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken)
    {
        return this.jObjectProcessor.EncryptAsync(input, encryptor, options, cancellationToken);
    }

    public Task EncryptAsync(Stream input, Stream output, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This overload is only supported for Stream JsonProcessor");
    }

    public async Task<(Stream, DecryptionContext)> DecryptAsync(Stream input, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) = this.InspectForMde(input);
        switch (status)
        {
            case MdePropertyStatus.Mde:
                {
                    DecryptionContext context = await this.jObjectProcessor.DecryptObjectAsync(itemJObj, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
                    await input.DisposeCompatAsync();

                    MemoryStream direct = new (capacity: 1024);
                    EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, direct);
                    return (direct, context);
                }

            case MdePropertyStatus.None:
            case MdePropertyStatus.LegacyOther:
            default:
                return (input, null);
        }
    }

    public async Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
    {
        (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) = this.InspectForMde(input);
        switch (status)
        {
            case MdePropertyStatus.Mde:
                {
                    DecryptionContext context = await this.jObjectProcessor.DecryptObjectAsync(itemJObj, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
                    output.Position = 0;
                    EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, output);
                    output.Position = 0;
                    await input.DisposeCompatAsync();
                    return context;
                }

            case MdePropertyStatus.None:
                if (input.CanSeek)
                {
                    input.Position = 0;
                }

                return null;
            case MdePropertyStatus.LegacyOther:
            default:
                return null;
        }
    }

    private (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) InspectForMde(Stream input)
    {
        input.Position = 0;
        JObject itemJObj = this.ReadJObject(input);
        JObject encryptionProperties = this.RetrieveEncryptionProperties(itemJObj);
        if (encryptionProperties == null)
        {
            input.Position = 0;
            return (MdePropertyStatus.None, null, null);
        }

        EncryptionProperties parsed = encryptionProperties.ToObject<EncryptionProperties>();
#pragma warning disable CS0618
        if (parsed.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
        {
            input.Position = 0;
            return (MdePropertyStatus.LegacyOther, null, null);
        }
#pragma warning restore CS0618

        return (MdePropertyStatus.Mde, itemJObj, parsed);
    }

    private JObject ReadJObject(Stream input)
    {
        using StreamReader sr = new (input, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        using Newtonsoft.Json.JsonTextReader jsonTextReader = new (sr)
        {
            ArrayPool = JsonArrayPool.Instance,
        };

        Newtonsoft.Json.JsonSerializerSettings settings = new ()
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
            MaxDepth = 64,
        };

        return Newtonsoft.Json.JsonSerializer.Create(settings).Deserialize<JObject>(jsonTextReader);
    }

    private JObject RetrieveEncryptionProperties(JObject item)
    {
        JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
        if (encryptionPropertiesJProp?.Value is JObject jObject)
        {
            return jObject;
        }

        return null;
    }
}
#endif