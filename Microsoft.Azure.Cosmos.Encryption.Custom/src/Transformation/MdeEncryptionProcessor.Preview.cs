// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal class MdeEncryptionProcessor
    {
        internal MdeJObjectEncryptionProcessor JObjectEncryptionProcessor { get; set; } =
            new MdeJObjectEncryptionProcessor();

#if NET8_0_OR_GREATER
        internal StreamProcessor StreamProcessor { get; set; } = new StreamProcessor();
#endif

        private IMdeJsonProcessorAdapter GetAdapter(JsonProcessor jsonProcessor)
        {
            return jsonProcessor switch
            {
                JsonProcessor.Newtonsoft => new NewtonsoftAdapter(this.JObjectEncryptionProcessor),
#if NET8_0_OR_GREATER
                JsonProcessor.Stream => new StreamAdapter(this.StreamProcessor),
#endif
                _ => throw new InvalidOperationException("Unsupported Json Processor"),
            };
        }

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JsonProcessor jsonProcessor = encryptionOptions.JsonProcessor;
            IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);

            if (diagnosticsContext != null)
            {
                using (diagnosticsContext.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + jsonProcessor))
                {
                    return await adapter.EncryptAsync(input, encryptor, encryptionOptions, cancellationToken);
                }
            }

            return await adapter.EncryptAsync(input, encryptor, encryptionOptions, cancellationToken);
        }

        internal Task<DecryptionContext> DecryptObjectAsync(
            JObject document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.JObjectEncryptionProcessor.DecryptObjectAsync(document, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
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
            using (diagnosticsContext.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor))
            {
                IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);
                return await adapter.DecryptAsync(input, encryptor, diagnosticsContext, cancellationToken);
            }
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
            using (diagnosticsContext.CreateScope(EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + jsonProcessor))
            {
                IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);
                return await adapter.DecryptAsync(input, output, encryptor, diagnosticsContext, cancellationToken);
            }
        }

        public async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JsonProcessor jsonProcessor = encryptionOptions.JsonProcessor;
            IMdeJsonProcessorAdapter adapter = this.GetAdapter(jsonProcessor);

            if (diagnosticsContext != null)
            {
                using (diagnosticsContext.CreateScope(EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + jsonProcessor))
                {
                    await adapter.EncryptAsync(input, output, encryptor, encryptionOptions, cancellationToken);
                    return;
                }
            }

            await adapter.EncryptAsync(input, output, encryptor, encryptionOptions, cancellationToken);
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

        public Task<DecryptionContext> DecryptStreamAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.StreamProcessor.DecryptStreamAsync(input, output, encryptor, properties, diagnosticsContext, cancellationToken);
        }

        private JsonProcessor GetRequestedJsonProcessor(RequestOptions requestOptions)
        {
            JsonProcessor jsonProcessor = JsonProcessor.Newtonsoft;
            if (JsonProcessorPropertyBag.TryGetJsonProcessorOverride(requestOptions, out JsonProcessor overrideProcessor))
            {
                jsonProcessor = overrideProcessor;
            }

            return jsonProcessor;
        }
#endif
    }
}
#endif