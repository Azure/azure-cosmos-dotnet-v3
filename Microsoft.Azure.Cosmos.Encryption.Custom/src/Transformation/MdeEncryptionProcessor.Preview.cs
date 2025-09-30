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
            JsonProcessor jsonProcessor = encryptionOptions.JsonProcessor;
            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + jsonProcessor);
            switch (jsonProcessor)
            {
                case JsonProcessor.Newtonsoft:
                    return await this.JObjectEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, token);
#if NET8_0_OR_GREATER
                case JsonProcessor.Stream:
                    MemoryStream ms = new ();
                    await this.StreamProcessor.EncryptStreamAsync(input, ms, encryptor, encryptionOptions, token);
                    return ms;
#endif
                default:
                    throw new InvalidOperationException("Unsupported JsonProcessor");
            }
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
            using CosmosDiagnosticsContext.Scope? selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor);

            return jsonProcessor switch
            {
                JsonProcessor.Newtonsoft => await this.DecryptNewtonsoftAsync(
                    input,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
#if NET8_0_OR_GREATER
                JsonProcessor.Stream => await this.DecryptUsingStreamProcessorAsync(
                    input,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
#endif
                _ => throw new InvalidOperationException("Unsupported JsonProcessor"),
            };
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
#if NET8_0_OR_GREATER
            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor);
#endif

            return jsonProcessor switch
            {
                JsonProcessor.Newtonsoft => await this.DecryptNewtonsoftToOutputAsync(
                    input,
                    output,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
#if NET8_0_OR_GREATER
                JsonProcessor.Stream => await this.DecryptUsingStreamApiAsync(
                    input,
                    output,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
#endif
                _ => throw new InvalidOperationException("Unsupported JsonProcessor"),
            };
        }

#if NET8_0_OR_GREATER
        private async Task<(Stream, DecryptionContext)> DecryptUsingStreamProcessorAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionProperties streamingProps = await ReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
            if (streamingProps == null)
            {
                // No encryption properties found - return original stream
                ResetStreamPosition(input);
                return (input, null);
            }

            return await this.DecryptStreamAsync(input, encryptor, streamingProps, diagnosticsContext, cancellationToken);
        }

        private async Task<DecryptionContext> DecryptUsingStreamApiAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionProperties streamingProps = await ReadMdeEncryptionPropertiesStreamingAsync(input, cancellationToken);
            if (streamingProps == null)
            {
                // No encryption properties found - no-op
                ResetStreamPosition(input);
                return null;
            }

            DecryptionContext context = await this.DecryptStreamAsync(input, output, encryptor, streamingProps, diagnosticsContext, cancellationToken);
            if (context == null)
            {
                ResetStreamPosition(input);
                return null;
            }

            await input.DisposeCompatAsync();
            return context;
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
            using StreamReader sr = new (input, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            using Newtonsoft.Json.JsonTextReader jsonTextReader = new (sr);

            jsonTextReader.ArrayPool = JsonArrayPool.Instance;
            Newtonsoft.Json.JsonSerializerSettings settings = new () { DateParseHandling = Newtonsoft.Json.DateParseHandling.None, MaxDepth = 64 };
            return Newtonsoft.Json.JsonSerializer.Create(settings).Deserialize<JObject>(jsonTextReader);
        }

        private static JObject RetrieveEncryptionProperties(JObject item)
        {
            JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
            if (encryptionPropertiesJProp?.Value is { Type: JTokenType.Object })
            {
                return (JObject)encryptionPropertiesJProp.Value;
            }

            return null;
        }

        private async Task<(Stream, DecryptionContext)> DecryptNewtonsoftAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) = InspectNewtonsoftForMde(input);

            return status switch
            {
                MdePropertyStatus.Mde or MdePropertyStatus.LegacyOther => await this.WriteNewtonsoftResultAsync(
                    input,
                    itemJObj,
                    encryptionProperties,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
                _ => (input, null),
            };
        }

        private async Task<(Stream, DecryptionContext)> WriteNewtonsoftResultAsync(
            Stream input,
            JObject itemJObj,
            EncryptionProperties encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DecryptionContext context = await this.JObjectEncryptionProcessor.DecryptObjectAsync(
                itemJObj,
                encryptor,
                encryptionProperties,
                diagnosticsContext,
                cancellationToken);

            await input.DisposeCompatAsync();

            MemoryStream direct = new (capacity: 1024);
            EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, direct);
            return (direct, context);
        }

        private async Task<DecryptionContext> DecryptNewtonsoftToOutputAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            (MdePropertyStatus status, JObject itemJObj, EncryptionProperties encryptionProperties) = InspectNewtonsoftForMde(input);

            return status switch
            {
                MdePropertyStatus.Mde or MdePropertyStatus.LegacyOther => await this.WriteNewtonsoftResultToOutputAsync(
                    input,
                    output,
                    itemJObj,
                    encryptionProperties,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
                _ => await this.HandleNotEncryptedAsync(input),
            };
        }

        private Task<DecryptionContext> HandleNotEncryptedAsync(Stream input)
        {
            ResetStreamPosition(input);
            return Task.FromResult<DecryptionContext>(null);
        }

        private static void ResetStreamPosition(Stream stream)
        {
            if (stream != null && stream.CanSeek)
            {
                stream.Position = 0;
            }
        }

        private async Task<DecryptionContext> WriteNewtonsoftResultToOutputAsync(
            Stream input,
            Stream output,
            JObject itemJObj,
            EncryptionProperties encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DecryptionContext context = await this.JObjectEncryptionProcessor.DecryptObjectAsync(
                itemJObj,
                encryptor,
                encryptionProperties,
                diagnosticsContext,
                cancellationToken);

            output.Position = 0;
            EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, output);
            output.Position = 0;
            await input.DisposeCompatAsync();
            return context;
        }

        private JsonProcessor GetRequestedJsonProcessor(RequestOptions requestOptions)
        {
#if NET8_0_OR_GREATER
            if (requestOptions != null && requestOptions.TryReadJsonProcessorOverride(out JsonProcessor overrideProcessor))
            {
                return overrideProcessor;
            }
#endif

            return JsonProcessor.Newtonsoft;
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
                return (MdePropertyStatus.LegacyOther, itemJObj, encryptionProperties);
            }
#pragma warning restore CS0618

            return (MdePropertyStatus.Mde, itemJObj, encryptionProperties);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Reads encryption properties from the stream using System.Text.Json streaming API.
        /// Returns null if no encryption properties are found.
        /// Throws NotSupportedException if legacy encryption algorithm is detected.
        /// </summary>
        private static async Task<EncryptionProperties> ReadMdeEncryptionPropertiesStreamingAsync(
            Stream input,
            CancellationToken cancellationToken)
        {
            input.Position = 0;
            EncryptionPropertiesWrapper properties = await System.Text.Json.JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
            input.Position = 0;
            if (properties?.EncryptionProperties == null)
            {
                return null;
            }

#pragma warning disable CS0618 // legacy algorithm check
            if (properties.EncryptionProperties.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                throw new NotSupportedException($"JsonProcessor.Stream is not supported for encryption algorithm '{properties.EncryptionProperties.EncryptionAlgorithm}'. Only '{CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized}' is supported with the Stream processor.");
            }
#pragma warning restore CS0618

            return properties.EncryptionProperties;
        }
#endif
    }
}
#endif