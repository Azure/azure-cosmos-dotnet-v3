//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Allows encrypting items in a container using Cosmos Legacy Encryption Algorithm and MDE Encryption Algorithm.
    /// </summary>
    internal static class EncryptionProcessor
    {
        internal static readonly JsonSerializerSettings JsonSerializerSettings = new ()
        {
            DateParseHandling = DateParseHandling.None,
        };

        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new (JsonSerializerSettings);

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        private static readonly StreamProcessor StreamProcessor = new ();
#endif

        private static readonly MdeEncryptionProcessor MdeEncryptionProcessor = new ();

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                return input;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            return encryptionOptions.EncryptionAlgorithm switch
            {
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized => await MdeEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, cancellationToken),
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized => await AeAesEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, cancellationToken),
                _ => throw new NotSupportedException($"Encryption Algorithm : {encryptionOptions.EncryptionAlgorithm} is not supported."),
            };
#pragma warning restore CS0618 // Type or member is obsolete
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        public static async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                await input.CopyToAsync(output, cancellationToken);
                return;
            }

            if (encryptionOptions.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)}");
            }

            if (encryptionOptions.JsonProcessor != JsonProcessor.Stream)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(JsonProcessor.Stream)}");
            }

            await EncryptionProcessor.StreamProcessor.EncryptStreamAsync(input, output, encryptor, encryptionOptions, cancellationToken);
        }
#endif

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (input, null);
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj = RetrieveItem(input);
            JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(itemJObj);

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return (input, null);
            }

            DecryptionContext decryptionContext = await DecryptInternalAsync(encryptor, diagnosticsContext, itemJObj, encryptionPropertiesJObj, cancellationToken);
#if NET8_0_OR_GREATER
            await input.DisposeAsync();
#else
            input.Dispose();
#endif
            return (BaseSerializer.ToStream(itemJObj), decryptionContext);
        }

        public static async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            JsonProcessor jsonProcessor,
            CancellationToken cancellationToken)
        {
            return jsonProcessor switch
            {
                JsonProcessor.Newtonsoft => await DecryptAsync(input, encryptor, diagnosticsContext, cancellationToken),
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                JsonProcessor.Stream => await DecryptStreamAsync(input, encryptor, diagnosticsContext, cancellationToken),
#endif
                _ => throw new InvalidOperationException("Unsupported Json Processor")
            };
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        public static async Task<DecryptionContext> DecryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            JsonProcessor jsonProcessor,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return null;
            }

            if (jsonProcessor != JsonProcessor.Stream)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(JsonProcessor.Stream)}");
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(output.CanWrite);
            Debug.Assert(output.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);
            input.Position = 0;

            EncryptionPropertiesWrapper properties = await System.Text.Json.JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
            input.Position = 0;
            if (properties?.EncryptionProperties == null)
            {
                await input.CopyToAsync(output, cancellationToken: cancellationToken);
                return null;
            }

            DecryptionContext context;
#pragma warning disable CS0618 // Type or member is obsolete
            if (properties.EncryptionProperties.EncryptionAlgorithm == CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                context = await StreamProcessor.DecryptStreamAsync(input, output, encryptor, properties.EncryptionProperties, diagnosticsContext, cancellationToken);
            }
            else if (properties.EncryptionProperties.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized)
            {
                (Stream stream, context) = await DecryptAsync(input, encryptor, diagnosticsContext, cancellationToken);
                await stream.CopyToAsync(output, cancellationToken);
                output.Position = 0;
            }
            else
            {
                input.Position = 0;
                throw new NotSupportedException($"Encryption Algorithm: {properties.EncryptionProperties.EncryptionAlgorithm} is not supported.");
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (context == null)
            {
                input.Position = 0;
                return null;
            }

            await input.DisposeAsync();
            return context;
        }
#endif

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        public static async Task<(Stream, DecryptionContext)> DecryptStreamAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (input, null);
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);
            input.Position = 0;

            EncryptionPropertiesWrapper properties = await System.Text.Json.JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
            input.Position = 0;
            if (properties?.EncryptionProperties == null)
            {
                return (input, null);
            }

            MemoryStream ms = new ();

            DecryptionContext context = await StreamProcessor.DecryptStreamAsync(input, ms, encryptor, properties.EncryptionProperties, diagnosticsContext, cancellationToken);
            if (context == null)
            {
                input.Position = 0;
                return (input, null);
            }

            await input.DisposeAsync();
            return (ms, context);
        }

#endif

        public static async Task<(JObject, DecryptionContext)> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);

            Debug.Assert(encryptor != null);

            JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(document);

            if (encryptionPropertiesJObj == null)
            {
                return (document, null);
            }

            DecryptionContext decryptionContext = await DecryptInternalAsync(encryptor, diagnosticsContext, document, encryptionPropertiesJObj, cancellationToken);

            return (document, decryptionContext);
        }

        private static async Task<DecryptionContext> DecryptInternalAsync(Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, JObject itemJObj, JObject encryptionPropertiesJObj, CancellationToken cancellationToken)
        {
            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
#pragma warning disable CS0618 // Type or member is obsolete
            DecryptionContext decryptionContext = encryptionProperties.EncryptionAlgorithm switch
            {
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized => await MdeEncryptionProcessor.DecryptObjectAsync(
                    itemJObj,
                    encryptor,
                    encryptionProperties,
                    diagnosticsContext,
                    cancellationToken),
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized => await AeAesEncryptionProcessor.DecryptContentAsync(
                    itemJObj,
                    encryptionProperties,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
                _ => throw new NotSupportedException($"Encryption Algorithm : {encryptionProperties.EncryptionAlgorithm} is not supported."),
            };
#pragma warning restore CS0618 // Type or member is obsolete
            return decryptionContext;
        }

        internal static DecryptionContext CreateDecryptionContext(
            List<string> pathsDecrypted,
            string dataEncryptionKeyId)
        {
            DecryptionInfo decryptionInfo = new (
                pathsDecrypted,
                dataEncryptionKeyId);

            DecryptionContext decryptionContext = new (
                new List<DecryptionInfo>() { decryptionInfo });

            return decryptionContext;
        }

        private static void ValidateInputForEncrypt(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(encryptor);
            ArgumentNullException.ThrowIfNull(encryptionOptions);
#else
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            if (encryptionOptions == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions));
            }
#endif

            encryptionOptions.Validate();
        }

        private static JObject RetrieveItem(
            Stream input)
        {
            Debug.Assert(input != null);

            JObject itemJObj;
            using (StreamReader sr = new (input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (JsonTextReader jsonTextReader = new (sr))
            {
                jsonTextReader.ArrayPool = JsonArrayPool.Instance;
                JsonSerializerSettings jsonSerializerSettings = new ()
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
                };

                itemJObj = Newtonsoft.Json.JsonSerializer.Create(jsonSerializerSettings).Deserialize<JObject>(jsonTextReader);
            }

            return itemJObj;
        }

        private static JObject RetrieveEncryptionProperties(
            JObject item)
        {
            JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp?.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            return encryptionPropertiesJObj;
        }

        internal static async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            Encryptor encryptor,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = BaseSerializer.FromStream<JObject>(content);

            if (contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is not JArray documents)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents");
            }

            foreach (JToken value in documents)
            {
                if (value is not JObject document)
                {
                    continue;
                }

                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);
                using (diagnosticsContext.CreateScope("EncryptionProcessor.DeserializeAndDecryptResponseAsync"))
                {
                    await DecryptAsync(
                        document,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                }
            }

            // the contents of contentJObj get decrypted in place for MDE algorithm model, and for legacy model _ei property is removed
            // and corresponding decrypted properties are added back in the documents.
            return BaseSerializer.ToStream(contentJObj);
        }
    }
}