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
        internal MdeJsonNodeEncryptionProcessor JsonNodeEncryptionProcessor { get; set; } = new MdeJsonNodeEncryptionProcessor();

        internal StreamProcessor StreamProcessor { get; set; } = new StreamProcessor();
#endif

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken token)
        {
#if NET8_0_OR_GREATER
            switch (encryptionOptions.JsonProcessor)
            {
                case JsonProcessor.Newtonsoft:
                    return await this.JObjectEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, token);

                case JsonProcessor.SystemTextJson:
                    return await this.JsonNodeEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, token);
                case JsonProcessor.Stream:
                    MemoryStream ms = new ();
                    await this.StreamProcessor.EncryptStreamAsync(input, ms, encryptor, encryptionOptions, token);
                    return ms;

                default:
                    throw new InvalidOperationException("Unsupported JsonProcessor");
            }
#else
            return encryptionOptions.JsonProcessor switch
            {
                JsonProcessor.Newtonsoft => await this.JObjectEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, token),
                _ => throw new InvalidOperationException("Unsupported JsonProcessor"),
            };
#endif
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        public async Task EncryptStreamAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken token)
        {
            switch (encryptionOptions.JsonProcessor)
            {
                case JsonProcessor.Newtonsoft:
                    await this.JObjectEncryptionProcessor.EncryptStreamAsync(input, output, encryptor, encryptionOptions, token);
                    break;
                case JsonProcessor.Stream:
                    await this.StreamProcessor.EncryptStreamAsync(input, output, encryptor, encryptionOptions, token);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported JsonProcessor {encryptionOptions.JsonProcessor}");
            }
        }
#endif

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
        internal async Task<DecryptionContext> DecryptObjectAsync(
            JsonNode document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return await this.JsonNodeEncryptionProcessor.DecryptObjectAsync(document, encryptor, encryptionProperties, diagnosticsContext, cancellationToken);
        }
#endif
    }
}
#endif