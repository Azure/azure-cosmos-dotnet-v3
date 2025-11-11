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

        public static async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
#if NET8_0_OR_GREATER
                await input.CopyToAsync(output, cancellationToken);
#else
                await input.CopyToAsync(output);
#endif
                return;
            }

            if (encryptionOptions.EncryptionAlgorithm != CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized)}");
            }

#if NET8_0_OR_GREATER
            if (encryptionOptions.JsonProcessor != JsonProcessor.Stream)
            {
                throw new NotSupportedException($"Streaming mode is only allowed for {nameof(JsonProcessor.Stream)}");
            }
#endif

            await MdeEncryptionProcessor.EncryptAsync(input, output, encryptor, encryptionOptions, diagnosticsContext, cancellationToken);
        }

        public static async Task EncryptAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            requestOptions.ResolveJsonProcessorSelection(encryptionOptions);
            await EncryptAsync(input, output, encryptor, encryptionOptions, diagnosticsContext, cancellationToken);
        }

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
            ArgumentValidation.ThrowIfNull(input);
            ArgumentValidation.ThrowIfNull(encryptor);
            ArgumentValidation.ThrowIfNull(encryptionOptions);

            encryptionOptions.Validate();
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

        /// <summary>
        /// Encrypts a stream and returns a new stream with encrypted content.
        /// The returned stream is a pooled MemoryStream that the caller must dispose.
        /// </summary>
        public static async Task<Stream> EncryptStreamAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken,
            string tag)
        {
            Stream output = MemoryStreamPool.GetStream(tag);
            bool success = false;
            try
            {
                await EncryptAsync(
                    input,
                    output,
                    encryptor,
                    encryptionOptions,
                    requestOptions,
                    diagnosticsContext,
                    cancellationToken);
                output.Position = 0;
                success = true;
                return output;
            }
            finally
            {
                if (!success)
                {
#if NET8_0_OR_GREATER
                    await output.DisposeAsync();
#else
                    output.Dispose();
#endif
                }
            }
        }

        /// <summary>
        /// Decrypts a stream and returns a new stream with decrypted content.
        /// The returned stream is a pooled MemoryStream that the caller must dispose.
        /// </summary>
        public static async Task<Stream> DecryptStreamAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken,
            string tag)
        {
            Stream output = MemoryStreamPool.GetStream(tag);
            bool success = false;
            try
            {
                await DecryptAsync(
                    input,
                    output,
                    encryptor,
                    diagnosticsContext,
                    requestOptions,
                    cancellationToken);
                output.Position = 0;
                success = true;
                return output;
            }
            finally
            {
                if (!success)
                {
#if NET8_0_OR_GREATER
                    await output.DisposeAsync();
#else
                    output.Dispose();
#endif
                }
            }
        }

        /// <summary>
        /// Synchronously encrypts a stream and returns a new stream with encrypted content.
        /// The returned stream is a pooled MemoryStream that the caller must dispose.
        /// Used by synchronous TransactionalBatch operations.
        /// </summary>
#pragma warning disable VSTHRD002 // Intentional synchronous wait for TransactionalBatch API
        public static Stream EncryptStreamSync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            string tag)
        {
            Stream output = MemoryStreamPool.GetStream(tag);
            EncryptAsync(
                input,
                output,
                encryptor,
                encryptionOptions,
                requestOptions,
                diagnosticsContext,
                cancellationToken: default).GetAwaiter().GetResult();
            output.Position = 0;
            return output;
        }
#pragma warning restore VSTHRD002
    }
}