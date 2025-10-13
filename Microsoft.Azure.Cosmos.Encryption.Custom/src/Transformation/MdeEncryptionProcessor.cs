// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
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

        private readonly Dictionary<JsonProcessor, Func<IMdeJsonProcessorAdapter>> adapterFactories;

        public MdeEncryptionProcessor()
        {
            this.adapterFactories = new Dictionary<JsonProcessor, Func<IMdeJsonProcessorAdapter>>
            {
                [JsonProcessor.Newtonsoft] = () => new NewtonsoftAdapter(this.JObjectEncryptionProcessor),
#if NET8_0_OR_GREATER
                [JsonProcessor.Stream] = () => new StreamAdapter(this.StreamProcessor),
#endif
            };
        }

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken token)
        {
            JsonProcessor jsonProcessor = encryptionOptions.JsonProcessor;
            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + jsonProcessor);

            IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);
            return await adapter.EncryptAsync(input, encryptor, encryptionOptions, token);
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

            JsonProcessor jsonProcessor = requestOptions.GetJsonProcessor(JsonProcessor.Newtonsoft);

            return await this.DecryptAsync(input, encryptor, jsonProcessor, diagnosticsContext, cancellationToken);
        }

        public async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            JsonProcessor jsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using CosmosDiagnosticsContext.Scope? selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor);

            IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);

            return await adapter.DecryptAsync(input, encryptor, diagnosticsContext, cancellationToken);
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

            JsonProcessor jsonProcessor = requestOptions.GetJsonProcessor(JsonProcessor.Newtonsoft);
            using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor);

            IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);
            return await adapter.DecryptAsync(input, output, encryptor, diagnosticsContext, cancellationToken);
        }

        public async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            if (encryptionOptions.JsonProcessor == JsonProcessor.Stream)
            {
                using IDisposable selectionScope = diagnosticsContext?.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream);
                IMdeJsonProcessorAdapter adapter = this.GetAdapter(JsonProcessor.Stream);
                await adapter.EncryptAsync(input, output, encryptor, encryptionOptions, cancellationToken);
                return;
            }
#endif

            // Fall back to Newtonsoft for netstandard2.0 or when Stream processor not requested
            IMdeJsonProcessorAdapter newtonsoftAdapter = this.GetAdapter(JsonProcessor.Newtonsoft);
            Stream encryptedStream = await newtonsoftAdapter.EncryptAsync(input, encryptor, encryptionOptions, cancellationToken);
            await encryptedStream.CopyToAsync(output);
            await encryptedStream.DisposeCompatAsync();
        }

#if NET8_0_OR_GREATER
        public async Task<Stream> DecryptJsonArrayStreamInPlaceAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            await this.StreamProcessor.DecryptJsonArrayStreamInPlaceAsync(input, encryptor, diagnosticsContext, cancellationToken);

            return input;
        }
#endif

        private readonly Dictionary<JsonProcessor, IMdeJsonProcessorAdapter> adapterCache = new Dictionary<JsonProcessor, IMdeJsonProcessorAdapter>();

        private IMdeJsonProcessorAdapter GetAdapter(JsonProcessor jsonProcessor)
        {
            if (!this.adapterCache.TryGetValue(jsonProcessor, out IMdeJsonProcessorAdapter adapter))
            {
                if (!this.adapterFactories.TryGetValue(jsonProcessor, out Func<IMdeJsonProcessorAdapter> factory))
                {
                    throw new NotSupportedException($"JsonProcessor '{jsonProcessor}' is not supported on this platform. Supported processors: {string.Join(", ", this.adapterFactories.Keys)}");
                }

                adapter = factory();
                this.adapterCache[jsonProcessor] = adapter;
            }

            return adapter;
        }
    }
}
