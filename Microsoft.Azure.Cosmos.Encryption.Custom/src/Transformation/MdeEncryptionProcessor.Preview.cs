// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.IO;
#if NET8_0_OR_GREATER
    using System.Text.Json.Nodes;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal class MdeEncryptionProcessor
    {
        internal MdeJObjectEncryptionProcessor JObjectEncryptionProcessor { get; set; } = new MdeJObjectEncryptionProcessor();

#if NET8_0_OR_GREATER
        internal StreamProcessor StreamProcessor { get; set; } = new StreamProcessor();
#endif

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken token)
        {
#if NET8_0_OR_GREATER
            JsonProcessor jsonProcessor = encryptionOptions.JsonProcessor;
            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + jsonProcessor);
            switch (jsonProcessor)
            {
                case JsonProcessor.Newtonsoft:
                    return await this.JObjectEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, token);
                case JsonProcessor.Stream:
                    MemoryStream ms = new ();
                    await this.StreamProcessor.EncryptStreamAsync(input, ms, encryptor, encryptionOptions, token);
                    return ms;

                default:
                    throw new InvalidOperationException("Unsupported JsonProcessor");
            }
#else
            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Newtonsoft);
            return await this.JObjectEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, token);
#endif
        }

        internal async Task<DecryptionContext> DecryptObjectAsync(
            JObject document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return await this.JObjectEncryptionProcessor.DecryptObjectAsync(document, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
        }

#if NET8_0_OR_GREATER
        public async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (input, null);
            }

            JsonProcessor jsonProcessor = this.GetRequestedJsonProcessor(requestOptions);

            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor);
            if (jsonProcessor == JsonProcessor.Newtonsoft)
            {
                return await this.HandleNewtonsoftDecryptAsync<(Stream, DecryptionContext)>(
                    input,
                    onMde: async item =>
                    {
                        DecryptionContext ctx = await this.JObjectEncryptionProcessor.DecryptObjectAsync(
                            item.itemJObj,
                            encryptor,
                            item.encryptionProperties,
                            diagnosticsContext,
                            cancellationToken);
                        await input.DisposeAsync();

                        // Direct serialization into freshly allocated stream.
                        MemoryStream direct = new (capacity: 1024);
                        EncryptionProcessor.BaseSerializer.WriteToStream(item.itemJObj, direct);
                        return (direct, ctx);
                    },
                    onNotEncrypted: () => Task.FromResult((input, (DecryptionContext)null)),
                    onLegacyOther: () => Task.FromResult((input, (DecryptionContext)null)));
            }

            this.ValidateSupportedStreamProcessor(jsonProcessor);

            (bool hasMde, EncryptionProperties streamingProps) = await TryReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
            if (!hasMde)
            {
                return (input, null);
            }

            return await this.DecryptStreamAsync(input, encryptor, streamingProps, diagnosticsContext, cancellationToken);
        }

        public async Task<DecryptionContext> DecryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return null;
            }

            JsonProcessor jsonProcessor = this.GetRequestedJsonProcessor(requestOptions);

            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor);
            if (jsonProcessor == JsonProcessor.Newtonsoft)
            {
                // If caller explicitly asked for Stream via property bag but encryption algorithm is legacy,
                // surface a clear NotSupportedException to align with test expectations.
                // Detect legacy algorithm presence for this decrypt attempt.
                if (requestOptions != null && requestOptions.TryReadJsonProcessorOverride(out JsonProcessor overrideProc) && overrideProc == JsonProcessor.Stream)
                {
                    // Peek to determine if payload is legacy (non-MDE) – reuse existing inspection helper.
                    if (input != null)
                    {
                        (bool isMde, _) = await TryReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
                        if (!isMde)
                        {
                            throw new NotSupportedException("JsonProcessor.Stream override is not supported for legacy/unencrypted documents when using provided output stream.");
                        }

                        if (input.CanSeek)
                        {
                            input.Position = 0; // reset for JObject path below
                        }
                    }
                }

                Func<(JObject itemJObj, EncryptionProperties encryptionProperties), Task<DecryptionContext>> onMde = async item =>
                {
                    DecryptionContext ctx = await this.JObjectEncryptionProcessor.DecryptObjectAsync(
                        item.itemJObj,
                        encryptor,
                        item.encryptionProperties,
                        diagnosticsContext,
                        cancellationToken);

                    output.Position = 0;
                    EncryptionProcessor.BaseSerializer.WriteToStream(item.itemJObj, output);
                    output.Position = 0;
                    await input.DisposeAsync();
                    return ctx;
                };

                Func<Task<DecryptionContext>> onNotEncrypted = () =>
                {
                    if (input.CanSeek)
                    {
                        input.Position = 0;
                    }

                    return Task.FromResult<DecryptionContext>(null); // not encrypted (no MDE properties)
                };

                Func<Task<DecryptionContext>> onLegacyOther = () => Task.FromResult<DecryptionContext>(null);

                return await this.HandleNewtonsoftDecryptAsync<DecryptionContext>(
                    input,
                    onMde,
                    onNotEncrypted,
                    onLegacyOther);
            }

            this.ValidateSupportedStreamProcessor(jsonProcessor);

            (bool hasMde, EncryptionProperties streamingProps) = await TryReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
            if (!hasMde)
            {
                // Need to determine if this is legacy-encrypted, MDE (but wrapper parse failed), or unencrypted.
                if (input.CanSeek)
                {
                    input.Position = 0;
                }

                JObject jObj = ReadJObject(input);
                JObject encryptionPropsJObj = RetrieveEncryptionProperties(jObj);
                if (encryptionPropsJObj == null)
                {
                    // Unencrypted – rewind and return null.
                    if (input.CanSeek)
                    {
                        input.Position = 0;
                    }

                    return null;
                }

                EncryptionProperties ep = encryptionPropsJObj.ToObject<EncryptionProperties>();
#pragma warning disable CS0618
                if (ep.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
                {
                    throw new NotSupportedException("JsonProcessor.Stream override is not supported for legacy encryption algorithms when using provided output stream.");
                }
#pragma warning restore CS0618

                // It is MDE, just not efficiently detectable by the streaming peek logic (e.g., format mismatch). Use JObject decrypt fallback.
                DecryptionContext jCtx = await this.JObjectEncryptionProcessor.DecryptObjectAsync(jObj, encryptor, ep, diagnosticsContext, cancellationToken);
                output.Position = 0;
                EncryptionProcessor.BaseSerializer.WriteToStream(jObj, output);
                output.Position = 0;
                if (input.CanSeek)
                {
                    input.Position = 0; // already consumed but reset for completeness
                }

                return jCtx;
            }

            DecryptionContext ctx = await this.DecryptStreamAsync(input, output, encryptor, streamingProps, diagnosticsContext, cancellationToken);
            if (ctx == null)
            {
                input.Position = 0;
                return null;
            }

            await input.DisposeAsync();
            return ctx;
        }

        public async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (encryptionOptions.JsonProcessor != JsonProcessor.Stream)
            {
                throw new NotSupportedException("This overload is only supported for Stream JsonProcessor");
            }

            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream);
            await this.StreamProcessor.EncryptStreamAsync(input, output, encryptor, encryptionOptions, cancellationToken);
        }

        public async Task<(Stream, DecryptionContext)> DecryptStreamAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            MemoryStream ms = new ();
            DecryptionContext context = await this.StreamProcessor.DecryptStreamAsync(input, ms, encryptor, properties, diagnosticsContext, cancellationToken);
            if (context == null)
            {
                return (input, null);
            }

            return (ms, context);
        }

        public async Task<DecryptionContext> DecryptStreamAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return await this.StreamProcessor.DecryptStreamAsync(input, output, encryptor, properties, diagnosticsContext, cancellationToken);
        }
#endif

        private static JObject ReadJObject(Stream input)
        {
            input.Position = 0;
            using (StreamReader sr = new (input, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (Newtonsoft.Json.JsonTextReader jsonTextReader = new (sr))
            {
                jsonTextReader.ArrayPool = JsonArrayPool.Instance;
                Newtonsoft.Json.JsonSerializerSettings settings = new () { DateParseHandling = Newtonsoft.Json.DateParseHandling.None, MaxDepth = 64 };
                return Newtonsoft.Json.JsonSerializer.Create(settings).Deserialize<JObject>(jsonTextReader);
            }
        }

        private static JObject RetrieveEncryptionProperties(JObject item)
        {
            JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
            if (encryptionPropertiesJProp?.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                return (JObject)encryptionPropertiesJProp.Value;
            }

            return null;
        }

#if NET8_0_OR_GREATER
        private JsonProcessor GetRequestedJsonProcessor(RequestOptions requestOptions)
        {
            JsonProcessor jsonProcessor = JsonProcessor.Newtonsoft;
            if (requestOptions.TryReadJsonProcessorOverride(out JsonProcessor overrideProcessor))
            {
                jsonProcessor = overrideProcessor;
            }

            return jsonProcessor;
        }

        private void ValidateSupportedStreamProcessor(JsonProcessor jsonProcessor)
        {
            if (jsonProcessor != JsonProcessor.Stream && jsonProcessor != JsonProcessor.Newtonsoft)
            {
                throw new InvalidOperationException("Unsupported Json Processor");
            }
        }

        private enum MdePropertyStatus
        {
            None,
            Mde,
            LegacyOther,
        }

        private static (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) InspectNewtonsoftForMde(Stream input)
        {
            input.Position = 0;
            JObject itemJObj = ReadJObject(input);
            JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(itemJObj);
            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return (MdePropertyStatus.None, null, null);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
#pragma warning disable CS0618
            if (encryptionProperties.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                input.Position = 0; // legacy algorithm
                return (MdePropertyStatus.LegacyOther, null, null);
            }
#pragma warning restore CS0618

            return (MdePropertyStatus.Mde, itemJObj, encryptionProperties);
        }

        private async Task<T> HandleNewtonsoftDecryptAsync<T>(
            Stream input,
            Func<(JObject itemJObj, EncryptionProperties encryptionProperties), Task<T>> onMde,
            Func<Task<T>> onNotEncrypted,
            Func<Task<T>> onLegacyOther)
        {
            (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) = InspectNewtonsoftForMde(input);
            return status switch
            {
                MdePropertyStatus.None => await onNotEncrypted(),
                MdePropertyStatus.LegacyOther => await onLegacyOther(),
                MdePropertyStatus.Mde => await onMde((itemJObj, encryptionProperties)),
                _ => await onNotEncrypted(),
            };
        }

        private static async Task<(bool hasMde, EncryptionProperties encryptionProperties)> TryReadMdeEncryptionPropertiesStreamingAsync(
            Stream input,
            CancellationToken cancellationToken)
        {
            input.Position = 0;
            EncryptionPropertiesWrapper properties = await System.Text.Json.JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
            input.Position = 0;
            if (properties?.EncryptionProperties == null)
            {
                return (false, null);
            }

#pragma warning disable CS0618 // legacy algorithm support
            if (properties.EncryptionProperties.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                // Gracefully signal no MDE so caller can fall back to legacy Newtonsoft path.
                return (false, null);
            }
#pragma warning restore CS0618

            return (true, properties.EncryptionProperties);
        }
#endif
    }
}
#endif