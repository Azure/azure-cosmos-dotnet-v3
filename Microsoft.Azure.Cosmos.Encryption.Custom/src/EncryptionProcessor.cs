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

        private static readonly MdeEncryptionProcessor MdeEncryptionProcessor = new ();

        public static Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return EncryptAsync(
                input,
                encryptor,
                requestOptions.EncryptionOptions,
                requestOptions.GetJsonProcessor(),
                diagnosticsContext,
                cancellationToken);
        }

        public static Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionTransactionalBatchItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return EncryptAsync(
                input,
                encryptor,
                requestOptions.EncryptionOptions,
                requestOptions.GetJsonProcessor(),
                diagnosticsContext,
                cancellationToken);
        }

#if NET8_0_OR_GREATER
        public static async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            JsonProcessor jsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions,
                jsonProcessor);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                await input.CopyToAsync(output, cancellationToken);

                return;
            }

            if (encryptionOptions.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)}");
            }

            if (jsonProcessor != JsonProcessor.Stream)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(JsonProcessor.Stream)}");
            }

            await MdeEncryptionProcessor.EncryptAsync(input, output, encryptor, encryptionOptions, jsonProcessor, diagnosticsContext, cancellationToken);
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

            return await DecryptParsedDocumentAsync(
                input,
                itemJObj,
                encryptionPropertiesJObj,
                encryptor,
                diagnosticsContext,
                cancellationToken);
        }

        public static Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return DecryptAsync(
                input,
                encryptor,
                requestOptions.GetJsonProcessor(),
                legacyFallback: true,
                diagnosticsContext,
                cancellationToken);
        }

        public static async Task<DecryptionContext> DecryptAsync(
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

            if (requestOptions.GetJsonProcessor() == JsonProcessor.Newtonsoft)
            {
                using (diagnosticsContext.CreateScope(
                    CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft))
                {
                    return await DecryptNewtonsoftAsync(
                        input,
                        output,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                }
            }

            JObject legacyDocument;
            JObject legacyEncryptionProperties;
            try
            {
                DecryptionContext context = await MdeEncryptionProcessor.DecryptAsync(
                    input,
                    output,
                    encryptor,
                    diagnosticsContext,
                    requestOptions,
                    cancellationToken);
                if (context != null ||
                    !TryGetLegacyEncryptedDocument(
                        input,
                        out legacyDocument,
                        out legacyEncryptionProperties))
                {
                    return context;
                }
            }
            catch (NotSupportedException)
            {
                input.Position = 0;
                if (!TryGetLegacyEncryptedDocument(
                    input,
                    out legacyDocument,
                    out legacyEncryptionProperties))
                {
                    throw;
                }
            }

            using (diagnosticsContext.CreateScope(
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft))
            {
                return await DecryptParsedDocumentAsync(
                    input,
                    output,
                    legacyDocument,
                    legacyEncryptionProperties,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        public static async Task<(Stream stream, DecryptionContext decryptableContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            JsonProcessor jsonProcessor,
            bool legacyFallback,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (null, null);
            }

            if (legacyFallback && jsonProcessor == JsonProcessor.Newtonsoft)
            {
                using (diagnosticsContext.CreateScope(
                    CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft))
                {
                    return await DecryptAsync(
                        input,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                }
            }

            try
            {
                (Stream stream, DecryptionContext context) = await MdeEncryptionProcessor.DecryptAsync(input, encryptor, jsonProcessor, diagnosticsContext, cancellationToken);
                if (context == null)
                {
                    if (legacyFallback &&
                        TryGetLegacyEncryptedDocument(
                            input,
                            out JObject legacyDocument,
                            out JObject legacyEncryptionProperties))
                    {
                        using (diagnosticsContext.CreateScope(
                            CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft))
                        {
                            return await DecryptParsedDocumentAsync(
                                input,
                                legacyDocument,
                                legacyEncryptionProperties,
                                encryptor,
                                diagnosticsContext,
                                cancellationToken);
                        }
                    }

                    input.Position = 0;
                    return (input, null);
                }

                await input.DisposeCompatAsync();

                return (stream, context);
            }
            catch (NotSupportedException)
            {
                if (legacyFallback)
                {
                    input.Position = 0;
                    if (TryGetLegacyEncryptedDocument(
                        input,
                        out JObject legacyDocument,
                        out JObject legacyEncryptionProperties))
                    {
                        using (diagnosticsContext.CreateScope(
                            CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft))
                        {
                            return await DecryptParsedDocumentAsync(
                                input,
                                legacyDocument,
                                legacyEncryptionProperties,
                                encryptor,
                                diagnosticsContext,
                                cancellationToken);
                        }
                    }
                }

                throw;
            }
        }

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

        private static bool TryGetLegacyEncryptedDocument(
            Stream input,
            out JObject document,
            out JObject encryptionProperties)
        {
            document = null;
            encryptionProperties = null;
            try
            {
                input.Position = 0;
                JObject parsedDocument = RetrieveItem(input);
                if (parsedDocument == null)
                {
                    return false;
                }

                JObject parsedEncryptionProperties = RetrieveEncryptionProperties(parsedDocument);
                if (parsedEncryptionProperties == null)
                {
                    return false;
                }

                string encryptionAlgorithm = (string)parsedEncryptionProperties[Constants.EncryptionAlgorithm];
#pragma warning disable CS0618 // Type or member is obsolete
                if (!string.Equals(
                    encryptionAlgorithm,
                    CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    StringComparison.Ordinal))
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    return false;
                }

                document = parsedDocument;
                encryptionProperties = parsedEncryptionProperties;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            finally
            {
                input.Position = 0;
            }
        }

        private static async Task<(Stream, DecryptionContext)> DecryptParsedDocumentAsync(
            Stream input,
            JObject document,
            JObject encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DecryptionContext context = await DecryptInternalAsync(
                encryptor,
                diagnosticsContext,
                document,
                encryptionProperties,
                cancellationToken);
            await input.DisposeCompatAsync();
            return (BaseSerializer.ToStream(document), context);
        }

        private static async Task<DecryptionContext> DecryptParsedDocumentAsync(
            Stream input,
            Stream output,
            JObject document,
            JObject encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            DecryptionContext context = await DecryptInternalAsync(
                encryptor,
                diagnosticsContext,
                document,
                encryptionProperties,
                cancellationToken);
            BaseSerializer.WriteToStream(document, output);
            output.Position = 0;
            await input.DisposeCompatAsync();
            return context;
        }

        private static async Task<DecryptionContext> DecryptNewtonsoftAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject document = RetrieveItem(input);
            JObject encryptionProperties = RetrieveEncryptionProperties(document);
            if (encryptionProperties == null)
            {
                input.Position = 0;
                return null;
            }

            return await DecryptParsedDocumentAsync(
                input,
                output,
                document,
                encryptionProperties,
                encryptor,
                diagnosticsContext,
                cancellationToken);
        }

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        private static async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            JsonProcessor jsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions,
                jsonProcessor);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                return input;
            }
#pragma warning disable CS0618 // Type or member is obsolete
            return encryptionOptions.EncryptionAlgorithm switch
            {
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized => await MdeEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, jsonProcessor, diagnosticsContext, cancellationToken),
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized => await AeAesEncryptionProcessor.EncryptAsync(input, encryptor, encryptionOptions, cancellationToken),
                _ => throw new NotSupportedException($"Encryption Algorithm : {encryptionOptions.EncryptionAlgorithm} is not supported."),
            };
#pragma warning restore CS0618 // Type or member is obsolete
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
            EncryptionOptions encryptionOptions,
            JsonProcessor jsonProcessor)
        {
            ArgumentValidation.ThrowIfNull(input);
            ArgumentValidation.ThrowIfNull(encryptor);
            ArgumentValidation.ThrowIfNull(encryptionOptions);

            encryptionOptions.Validate(jsonProcessor);
        }

        private static JObject RetrieveItem(
            Stream input)
        {
            Debug.Assert(input != null);

            using StreamReader sr = new (input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            using JsonTextReader jsonTextReader = new (sr);
            jsonTextReader.ArrayPool = JsonArrayPool.Instance;
            JsonSerializerSettings jsonSerializerSettings = new ()
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
            };

            return Newtonsoft.Json.JsonSerializer.Create(jsonSerializerSettings).Deserialize<JObject>(jsonTextReader);
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

        internal static Task<List<DecryptableItem>> ConvertResponseToDecryptableItemsAsync(
            Stream content,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            JsonProcessor jsonProcessor,
            CancellationToken cancellationToken)
        {
            ArgumentValidation.ThrowIfNull(content);
            ArgumentValidation.ThrowIfNull(encryptor);
            ArgumentValidation.ThrowIfNull(cosmosSerializer);

            return jsonProcessor switch
            {
#if NET8_0_OR_GREATER
                JsonProcessor.Stream => ConvertResponseToDecryptableItemsStreamAsync(content, encryptor, cosmosSerializer, cancellationToken),
#endif
                JsonProcessor.Newtonsoft => Task.FromResult(ConvertResponseToDecryptableItemsNewtonsoft(content, encryptor, cosmosSerializer)),
                _ => throw new NotImplementedException(),
            };
        }

        internal static async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            Encryptor encryptor,
            JsonProcessor jsonProcessor,
            CancellationToken cancellationToken)
        {
            return jsonProcessor switch
            {
#if NET8_0_OR_GREATER
                JsonProcessor.Stream => await DecryptJsonArrayStreamAsync(content, encryptor, cancellationToken),
#endif
                _ => await DecryptJsonArrayNewtonsoftAsync(content, encryptor, cancellationToken),
            };
        }

#if NET8_0_OR_GREATER
        private static async Task<List<DecryptableItem>> ConvertResponseToDecryptableItemsStreamAsync(
            Stream content,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            CancellationToken cancellationToken)
        {
            List<DecryptableItem> decryptableItems = new ();

            try
            {
                await foreach (Stream itemStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(content, cancellationToken).ConfigureAwait(false))
                {
                    StreamDecryptableItem item = new (
                        itemStream,
                        encryptor,
                        cosmosSerializer);

                    decryptableItems.Add(item);
                }

                return decryptableItems;
            }
            catch
            {
                // If the splitter throws after yielding one or more documents, every StreamDecryptableItem
                // already in the list owns a pooled buffer the caller will never see (so cannot dispose).
                // Drain the partial list before re-throwing so those buffers are returned and cleared.
                foreach (DecryptableItem partialItem in decryptableItems)
                {
                    if (partialItem is IAsyncDisposable asyncDisposable)
                    {
                        try
                        {
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // Swallow per-item disposal failures so the remaining orphans are still
                            // drained and the original cause is rethrown.
                        }
                    }
                }

                throw;
            }
        }
#endif

        private static List<DecryptableItem> ConvertResponseToDecryptableItemsNewtonsoft(
            Stream content,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            JObject contentJObj = BaseSerializer.FromStream<JObject>(content);

            if (contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is not JArray documents)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed Response did not have an array of Documents.");
            }

            List<DecryptableItem> decryptableItems = new (documents.Count);

            foreach (JToken value in documents)
            {
                DecryptableItemCore item = new (
                    value,
                    encryptor,
                    cosmosSerializer);

                decryptableItems.Add(item);
            }

            return decryptableItems;
        }

#if NET8_0_OR_GREATER
        private static async Task<Stream> DecryptJsonArrayStreamAsync(
            Stream content,
            Encryptor encryptor,
            CancellationToken cancellationToken)
        {
            try
            {
                return await MdeEncryptionProcessor.DecryptJsonArrayStreamInPlaceAsync(
                    content,
                    encryptor,
                    CosmosDiagnosticsContext.Create(null),
                    cancellationToken);
            }
            catch (NotSupportedException)
            {
                content.Position = 0;

                return await DecryptJsonArrayNewtonsoftAsync(content, encryptor, cancellationToken);
            }
        }
#endif

        private static async Task<Stream> DecryptJsonArrayNewtonsoftAsync(Stream content, Encryptor encryptor, CancellationToken cancellationToken)
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
                await DecryptAsync(
                    document,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken);
            }

            // the contents of contentJObj get decrypted in place for MDE algorithm model, and for legacy model _ei property is removed
            // and corresponding decrypted properties are added back in the documents.
            return BaseSerializer.ToStream(contentJObj);
        }
    }
}