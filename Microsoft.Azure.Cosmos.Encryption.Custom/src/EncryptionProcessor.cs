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

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> EncryptAsync(
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

        public static Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionItemRequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return EncryptAsync(
                input,
                encryptor,
                requestOptions.EncryptionOptions,
                requestOptions.GetJsonProcessor(defaultJsonProcessor),
                diagnosticsContext,
                cancellationToken);
        }

        public static Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionTransactionalBatchItemRequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return EncryptAsync(
                input,
                encryptor,
                requestOptions.EncryptionOptions,
                requestOptions.GetJsonProcessor(defaultJsonProcessor),
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

            DecryptionContext decryptionContext = await DecryptInternalAsync(encryptor, diagnosticsContext, itemJObj, encryptionPropertiesJObj, cancellationToken);
            await input.DisposeCompatAsync();

            return (BaseSerializer.ToStream(itemJObj), decryptionContext);
        }

        public static async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (input, null);
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            // Try to peek at the content to check if it's legacy encryption algorithm
            // Some streams (e.g., those that only support async reads or contain malformed JSON) may throw exceptions
            // during synchronous peeking. In such cases, delegate directly to MdeEncryptionProcessor.
            try
            {
                JObject itemJObj = RetrieveItem(input);
                JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(itemJObj);

                if (encryptionPropertiesJObj != null)
                {
                    // Parse encryption properties to check the algorithm
                    EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();

#pragma warning disable CS0618 // Type or member is obsolete
                    if (string.Equals(encryptionProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, StringComparison.Ordinal))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        // Use legacy decryption for AEAes256CbcHmacSha256Randomized
                        DecryptionContext decryptionContext = await DecryptInternalAsync(encryptor, diagnosticsContext, itemJObj, encryptionPropertiesJObj, cancellationToken);
                        await input.DisposeCompatAsync();
                        return (BaseSerializer.ToStream(itemJObj), decryptionContext);
                    }
                }

                // For MDE algorithm or no encryption properties, delegate to MdeEncryptionProcessor
                input.Position = 0;
            }
            catch
            {
                // Stream doesn't support synchronous reads, contains malformed JSON, or other parsing error.
                // Reset position and delegate to MdeEncryptionProcessor which uses async reads and will handle errors appropriately.
                input.Position = 0;
            }

            return await MdeEncryptionProcessor.DecryptAsync(input, encryptor, diagnosticsContext, requestOptions, defaultJsonProcessor, cancellationToken);
        }

        public static async Task<DecryptionContext> DecryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            CancellationToken cancellationToken)
        {
            return await MdeEncryptionProcessor.DecryptAsync(input, output, encryptor, diagnosticsContext, requestOptions, defaultJsonProcessor, cancellationToken);
        }

#if NET8_0_OR_GREATER
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

            EncryptionPropertiesWrapper properties = await PooledJsonSerializer.DeserializeFromStreamAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
            input.Position = 0;
            if (properties?.EncryptionProperties == null)
            {
                return (input, null);
            }

            PooledMemoryStream ms = new ();
            try
            {
                DecryptionContext context = await MdeEncryptionProcessor.DecryptStreamAsync(input, ms, encryptor, properties.EncryptionProperties, diagnosticsContext, cancellationToken);
                if (context == null)
                {
                    // CRITICAL: Must dispose PooledMemoryStream to prevent memory leak
                    await ms.DisposeAsync();
                    input.Position = 0;
                    return (input, null);
                }

                await input.DisposeAsync();
                return (ms, context);  // Ownership transfers successfully
            }
            catch
            {
                // CRITICAL: Dispose PooledMemoryStream on exception to prevent memory leak
                await ms.DisposeAsync();
                throw;  // Rethrow to preserve original exception
            }
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

        /// <summary>
        /// Decrypts a single document already materialized as a <see cref="JObject"/> (for example the
        /// change-feed-processor typed handlers, which surface documents as <see cref="JObject"/>) honoring the
        /// container-wide <paramref name="defaultJsonProcessor"/>.
        /// </summary>
        /// <remarks>
        /// When the effective processor is <c>JsonProcessor.Stream</c> the MDE document is decrypted through
        /// the System.Text.Json streaming adapter (emitting the <c>EncryptionProcessor.Decrypt.Mde.Stream</c>
        /// diagnostics scope), matching the point-read / feed-read semantics instead of silently dropping to the
        /// Newtonsoft (JObject) decryptor. Legacy AEAD documents and the Newtonsoft default keep the existing JObject
        /// decrypt behavior. This path carries no per-request <see cref="RequestOptions"/>, so only the container
        /// default selects the processor.
        /// </remarks>
        public static async Task<(JObject, DecryptionContext)> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            JsonProcessor defaultJsonProcessor,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);
            Debug.Assert(encryptor != null);

#if NET8_0_OR_GREATER
            if (defaultJsonProcessor == JsonProcessor.Stream)
            {
                Stream documentStream = BaseSerializer.ToStream(document);
                Stream decryptedStream = null;
                try
                {
                    DecryptionContext decryptionContext;
                    (decryptedStream, decryptionContext) = await DecryptAsync(
                        documentStream,
                        encryptor,
                        diagnosticsContext,
                        requestOptions: null,
                        defaultJsonProcessor,
                        cancellationToken);

                    return decryptionContext != null
                        ? (BaseSerializer.FromStream<JObject>(decryptedStream), decryptionContext)
                        : (document, null);
                }
                finally
                {
                    if (decryptedStream != null && !ReferenceEquals(decryptedStream, documentStream))
                    {
                        await decryptedStream.DisposeCompatAsync();
                    }

                    await documentStream.DisposeCompatAsync();
                }
            }
#endif

            return await DecryptAsync(
                document,
                encryptor,
                diagnosticsContext,
                cancellationToken);
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

        /// <remarks>
        /// Decrypts every document in a feed / query / change-feed response body. The JSON processor used for
        /// the per-document decryption is resolved from <paramref name="requestOptions"/> (per-request override
        /// via <see cref="RequestOptions.Properties"/>) falling back to <paramref name="defaultJsonProcessor"/>
        /// (the container-wide default). This keeps the feed/query decrypt path consistent with the point-read
        /// and write paths: when the effective processor is <c>JsonProcessor.Stream</c> the MDE documents are
        /// decrypted through the System.Text.Json streaming adapter rather than silently dropping to Newtonsoft.
        /// </remarks>
        internal static async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            Encryptor encryptor,
            RequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            JsonProcessor jsonProcessor = requestOptions != null
                ? requestOptions.GetJsonProcessor(defaultJsonProcessor)
                : defaultJsonProcessor;

            if (jsonProcessor == JsonProcessor.Stream)
            {
                // Stream (System.Text.Json) processor: decrypt the Documents array straight from the response
                // bytes, splicing each document's decrypted bytes back into the envelope without materializing the
                // page - or any individual document - as an intermediate Newtonsoft JObject. Per-document AEAD
                // detection, MDE streaming decryption, and the Decrypt.Mde.Stream diagnostics scope reuse the same
                // adapter machinery as point reads, preserving exact parity (see DecryptFeedDocumentWithStreamProcessorAsync).
                return await StreamFeedProcessor.DecryptResponseAsync(
                    content,
                    (documentStream, diagnosticsContext) => DecryptFeedDocumentWithStreamProcessorAsync(
                        documentStream,
                        encryptor,
                        diagnosticsContext,
                        requestOptions,
                        defaultJsonProcessor,
                        cancellationToken),
                    requestOptions,
                    cancellationToken);
            }
#endif

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

                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
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

#if NET8_0_OR_GREATER
        /// <summary>
        /// Decrypts a single feed / query / change-feed document under the Stream (System.Text.Json) processor
        /// without materializing the document as an intermediate Newtonsoft <see cref="JObject"/>.
        /// </summary>
        /// <remarks>
        /// The single-document entry point
        /// (<see cref="DecryptAsync(Stream, Encryptor, CosmosDiagnosticsContext, RequestOptions, JsonProcessor, CancellationToken)"/>)
        /// parses every document into a Newtonsoft <see cref="JObject"/> purely to detect the legacy AEAD algorithm
        /// before delegating MDE / unencrypted documents to the streaming adapter. On the feed hot path that per-document
        /// parse defeats the Stream processor's allocation benefit, so this variant detects AEAD by streaming only the
        /// <c>_ei</c> metadata - using the exact same reader and algorithm gate the Stream adapter itself applies.
        /// Because the gate is identical to the adapter's, any document routed to the streaming path is one the adapter
        /// also accepts (it can never reach the adapter's AEAD throw). Behavior is otherwise identical to the
        /// single-document entry point: legacy AEAD documents fall back to it (and its Newtonsoft decryption), while MDE
        /// and unencrypted documents flow through the same <see cref="MdeEncryptionProcessor"/> streaming call - emitting
        /// the same <c>Decrypt.Mde.Stream</c> scope and returning a <see langword="null"/> context for pass-through
        /// (unencrypted) documents.
        /// </remarks>
        private static async Task<(Stream, DecryptionContext)> DecryptFeedDocumentWithStreamProcessorAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            CancellationToken cancellationToken)
        {
            // Detect a legacy AEAD document by streaming only the _ei metadata, using the same reader and algorithm
            // gate the Stream adapter applies - so a document we route to the streaming path is one the adapter accepts.
            bool isLegacyAeadDocument = false;
            try
            {
                EncryptionProperties encryptionProperties = await EncryptionPropertiesStreamReader.ReadAsync(
                    input,
                    PooledJsonSerializer.SerializerOptions,
                    cancellationToken).ConfigureAwait(false);

#pragma warning disable CS0618 // Type or member is obsolete
                isLegacyAeadDocument = encryptionProperties != null
                    && string.Equals(encryptionProperties.EncryptionAlgorithm, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized, StringComparison.Ordinal);
#pragma warning restore CS0618 // Type or member is obsolete
            }
            catch
            {
                // Malformed JSON or a stream that does not support the streaming peek: fall back to the single-document
                // entry point, which tolerates these cases via its own try/catch and async reads (matching its behavior).
            }
            finally
            {
                if (input.CanSeek)
                {
                    input.Position = 0;
                }
            }

            if (isLegacyAeadDocument)
            {
                // Legacy AEAD is unsupported by the Stream processor by design; route through the single-document entry
                // point, which performs the Newtonsoft-based legacy decryption - exactly as the feed path did before.
                return await DecryptAsync(input, encryptor, diagnosticsContext, requestOptions, defaultJsonProcessor, cancellationToken);
            }

            // MDE, unencrypted (no _ei), or non-object _ei: decrypt straight through the streaming adapter, exactly as the
            // single-document entry point does for these documents (same scope, same null-context pass-through), but
            // without the per-document Newtonsoft parse.
            return await MdeEncryptionProcessor.DecryptAsync(input, encryptor, diagnosticsContext, requestOptions, defaultJsonProcessor, cancellationToken);
        }
#endif
    }
}