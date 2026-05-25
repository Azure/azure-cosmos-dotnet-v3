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
#if NET8_0_OR_GREATER
    using System.Buffers;
#endif
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

            DecryptionContext decryptionContext = await DecryptInternalAsync(encryptor, diagnosticsContext, itemJObj, encryptionPropertiesJObj, cancellationToken);
            await input.DisposeCompatAsync();

            return (BaseSerializer.ToStream(itemJObj), decryptionContext);
        }

        public static async Task<(Stream, DecryptionContext)> DecryptAsync(
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

            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

#if NET8_0_OR_GREATER
            // When the caller has opted into JsonProcessor.Stream, replace the Newtonsoft JObject peek
            // (which exists solely to detect legacy AE-AES documents) with a Utf8JsonReader-based
            // detector. The detector classifies the document without allocating either a JObject or
            // an EncryptionPropertiesWrapper, and routes Mde / unencrypted documents directly to
            // MdeEncryptionProcessor while preserving the documented contract that legacy AE-AES
            // documents transparently fall back to the Newtonsoft path
            // (regression: EncryptionProcessorTests.Decrypt_StreamSelection_LegacyAlgorithm_FallsBackToNewtonsoft).
            if (requestOptions != null
                && requestOptions.TryReadJsonProcessorOverride(out JsonProcessor overrideProcessor)
                && overrideProcessor == JsonProcessor.Stream)
            {
                LegacyAlgorithmDetector.DetectionResult detection = TryDetectAlgorithm(input);
                switch (detection)
                {
                    case LegacyAlgorithmDetector.DetectionResult.MdeAlgorithm:
                    case LegacyAlgorithmDetector.DetectionResult.NotEncrypted:
                        input.Position = 0;
                        return await MdeEncryptionProcessor.DecryptAsync(input, encryptor, diagnosticsContext, requestOptions, cancellationToken);

                    case LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm:
                    case LegacyAlgorithmDetector.DetectionResult.Unknown:
                    default:
                        // Fall through to the legacy JObject peek path below. Ask it to strip the
                        // Stream override before its post-peek MDE fallthrough call — but only when
                        // that fallthrough is reached via the success branch (i.e. JObject parsing
                        // worked). If JObject parsing throws (async-only stream, malformed payload),
                        // the catch branch needs to keep the original requestOptions so MDE can use
                        // its async-capable Stream adapter to read the stream and honor cancellation.
                        return await DecryptViaJObjectPeekAsync(input, encryptor, diagnosticsContext, requestOptions, cancellationToken, stripStreamOverrideOnSuccessFallthrough: true);
                }
            }
#endif

            return await DecryptViaJObjectPeekAsync(input, encryptor, diagnosticsContext, requestOptions, cancellationToken);
        }

#if NET8_0_OR_GREATER
        private static async Task<(Stream, DecryptionContext)> DecryptViaJObjectPeekAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken,
            bool stripStreamOverrideOnSuccessFallthrough = false)
#else
        private static async Task<(Stream, DecryptionContext)> DecryptViaJObjectPeekAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
#endif
        {
            // Try to peek at the content to check if it's legacy encryption algorithm
            // Some streams (e.g., those that only support async reads or contain malformed JSON) may throw exceptions
            // during synchronous peeking. In such cases, delegate directly to MdeEncryptionProcessor.
#if NET8_0_OR_GREATER
            bool jobjectParseSucceeded = false;
#endif
            try
            {
                JObject itemJObj = RetrieveItem(input);
#if NET8_0_OR_GREATER
                jobjectParseSucceeded = true;
#endif
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

#if NET8_0_OR_GREATER
            // For Stream-opt-in callers: when the JObject peek successfully read the document synchronously,
            // strip the JsonProcessor.Stream override for the fallthrough MDE call so it uses the Newtonsoft
            // adapter. That guarantees byte-for-byte parity with non-opt-in callers on shapes the streaming
            // MDE adapter would otherwise reject (e.g. {"_ei":{"_ea":""}}, missing _ea, empty _ei).
            // If the peek FAILED (catch branch), keep the original override: the input is likely an
            // async-only stream and MDE needs its streaming adapter to read it / honor cancellation.
            if (jobjectParseSucceeded
                && stripStreamOverrideOnSuccessFallthrough
                && requestOptions != null
                && requestOptions.Properties != null
                && requestOptions.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey))
            {
                IReadOnlyDictionary<string, object> originalProperties = requestOptions.Properties;
                Dictionary<string, object> stripped = new (originalProperties.Count);
                foreach (KeyValuePair<string, object> kvp in originalProperties)
                {
                    if (kvp.Key != JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey)
                    {
                        stripped[kvp.Key] = kvp.Value;
                    }
                }

                requestOptions.Properties = stripped;
                try
                {
                    return await MdeEncryptionProcessor.DecryptAsync(input, encryptor, diagnosticsContext, requestOptions, cancellationToken);
                }
                finally
                {
                    requestOptions.Properties = originalProperties;
                }
            }
#endif

            return await MdeEncryptionProcessor.DecryptAsync(input, encryptor, diagnosticsContext, requestOptions, cancellationToken);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Reads <paramref name="input"/> into a span backed either by the MemoryStream's underlying buffer
        /// (zero-copy fast path) or a pooled rented array, runs <see cref="LegacyAlgorithmDetector.Detect"/>,
        /// and always restores <c>input.Position</c> to the value it had on entry. Any I/O error collapses
        /// to <see cref="LegacyAlgorithmDetector.DetectionResult.Unknown"/> so the caller falls back to the
        /// robust JObject path.
        /// </summary>
        private static LegacyAlgorithmDetector.DetectionResult TryDetectAlgorithm(Stream input)
        {
            long startPosition = input.Position;
            try
            {
                if (input is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
                {
                    ReadOnlySpan<byte> documentBytes = segment.AsSpan((int)startPosition, segment.Count - (int)startPosition);
                    return LegacyAlgorithmDetector.Detect(documentBytes);
                }

                long remainingLong = input.Length - startPosition;
                if (remainingLong <= 0 || remainingLong > int.MaxValue)
                {
                    return LegacyAlgorithmDetector.DetectionResult.Unknown;
                }

                int remaining = (int)remainingLong;
                byte[] rented = ArrayPool<byte>.Shared.Rent(remaining);
                try
                {
                    int read = 0;
                    while (read < remaining)
                    {
                        int n = input.Read(rented, read, remaining - read);
                        if (n == 0)
                        {
                            break;
                        }

                        read += n;
                    }

                    return LegacyAlgorithmDetector.Detect(new ReadOnlySpan<byte>(rented, 0, read));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
            catch
            {
                return LegacyAlgorithmDetector.DetectionResult.Unknown;
            }
            finally
            {
                input.Position = startPosition;
            }
        }
#endif

        public static async Task<DecryptionContext> DecryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return await MdeEncryptionProcessor.DecryptAsync(input, output, encryptor, diagnosticsContext, requestOptions, cancellationToken);
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

            EncryptionPropertiesWrapper properties = await System.Text.Json.JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(input, cancellationToken: cancellationToken);
            input.Position = 0;
            if (properties?.EncryptionProperties == null)
            {
                return (input, null);
            }

            MemoryStream ms = new ();

            DecryptionContext context = await MdeEncryptionProcessor.DecryptStreamAsync(input, ms, encryptor, properties.EncryptionProperties, diagnosticsContext, cancellationToken);
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