//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

#if PREVIEW
    public
#else
    internal
#endif
    class EncryptionStreamTransformer : CosmosStreamTransformer
    {
        private static readonly CosmosSerializer baseSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());
        private IReadOnlyList<string> EncryptedPaths;
        private IReadOnlyList<string> DecryptedPaths;

        public Encryptor Encryptor { get; }

        public EncryptionOptions EncryptionOptions { get; }

        public Action<Stream, Exception> ErrorHandler { get; }
                
        public EncryptionStreamTransformer(
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            Action<Stream, Exception> errorHandler = null)
        {
            if(encryptor == null)
            {
                throw new ArgumentNullException(ClientResources.EncryptorNotConfigured);
            }

            this.Encryptor = encryptor;
            this.EncryptionOptions = encryptionOptions;
            this.ErrorHandler = errorHandler;
            this.EncryptedPaths = new List<string>();
            this.DecryptedPaths = new List<string>();
        }

        public override async Task<Stream> TransformRequestItemStreamAsync(Stream input, StreamTransformationContext context, CancellationToken cancellationToken)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            if (input == null)
            {
                return null;
            }

            using (diagnosticsContext.CreateScope("Encrypt"))
            {
                return await this.EncryptAsync(
                    input,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        public override async Task<Stream> TransformResponseItemStreamAsync(Stream input, StreamTransformationContext context, CancellationToken cancellationToken)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            if (input == null)
            {
                return null;
            }

            try
            {
                using (diagnosticsContext.CreateScope("Decrypt"))
                {
                    return await this.DecryptAsync(
                        input,
                        diagnosticsContext,
                        cancellationToken);
                }
            }
            catch(Exception ex)
            {
                input.Position = 0;
                if (this.ErrorHandler != null)
                {
                    Stream output = new MemoryStream();
                    input.CopyTo(output);
                    input.Position = 0;
                    output.Position = 0;
                    this.ErrorHandler(output, ex);
                }
                return input;
            }
        }

        private async Task<Stream> EncryptAsync(
            Stream input,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(diagnosticsContext != null);
            Debug.Assert(this.EncryptionOptions != null);

            if (string.IsNullOrEmpty(this.EncryptionOptions.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(this.EncryptionOptions.DataEncryptionKeyId));
            }

            if (string.IsNullOrEmpty(this.EncryptionOptions.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(this.EncryptionOptions.EncryptionAlgorithm));
            }

            if (this.EncryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(this.EncryptionOptions.PathsToEncrypt));
            }

            if (this.EncryptionOptions.PathsToEncrypt.Count == 0)
            {
                return input;
            }

            foreach (string path in this.EncryptionOptions.PathsToEncrypt)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
                {
                    throw new ArgumentException($"Invalid path {path ?? string.Empty}", nameof(this.EncryptionOptions.PathsToEncrypt));
                }
            }

            JObject itemJObj = EncryptionStreamTransformer.baseSerializer.FromStream<JObject>(input);

            JObject toEncryptJObj = new JObject();
            List<string> encryptedPaths = new List<string>();
            
            foreach (string pathToEncrypt in this.EncryptionOptions.PathsToEncrypt)
            {
                string propertyName = pathToEncrypt.Substring(1);
                JToken propertyValueHolder = itemJObj.Property(propertyName).Value;

                // Even null in the JSON is a JToken with Type Null, this null check is just a sanity check
                if (propertyValueHolder != null)
                {
                    toEncryptJObj.Add(propertyName, itemJObj.Property(propertyName).Value.Value<JToken>());
                    itemJObj.Remove(propertyName);
                    encryptedPaths.Add("/" + propertyName);
                }
            }

            MemoryStream memoryStream = EncryptionStreamTransformer.baseSerializer.ToStream<JObject>(toEncryptJObj) as MemoryStream;
            Debug.Assert(memoryStream != null);
            Debug.Assert(memoryStream.TryGetBuffer(out _));

            byte[] plainText = memoryStream.GetBuffer();
            byte[] cipherText = await this.Encryptor.EncryptAsync(
                plainText,
                this.EncryptionOptions.DataEncryptionKeyId,
                this.EncryptionOptions.EncryptionAlgorithm,
                cancellationToken);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(EncryptAsync)}.");
            }

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                encryptionFormatVersion: 2,
                dataEncryptionKeyId: this.EncryptionOptions.DataEncryptionKeyId,
                encryptionAlgorithm: this.EncryptionOptions.EncryptionAlgorithm,
                encryptedData: cipherText);

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
            this.EncryptedPaths = encryptedPaths;
            Debug.Assert(this.EncryptedPaths.Count == this.EncryptionOptions.PathsToEncrypt.Count);

            return EncryptionStreamTransformer.baseSerializer.ToStream(itemJObj);
        }

        private async Task<Stream> DecryptAsync(
            Stream input,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
                }
            }

            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return input;
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();

            JObject plainTextJObj = await this.DecryptContentAsync(
                encryptionProperties,
                diagnosticsContext,
                cancellationToken);

            List<string> decryptedPaths = new List<string>();
            foreach (JProperty property in plainTextJObj.Properties())
            {
                itemJObj.Add(property.Name, property.Value);
                decryptedPaths.Add("/" + property.Name);
            }

            itemJObj.Remove(Constants.EncryptedInfo);
            this.DecryptedPaths = decryptedPaths;
            return EncryptionStreamTransformer.baseSerializer.ToStream(itemJObj);
        }

        private async Task<CosmosObject> DecryptAsync(
            CosmosObject document,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);
            Debug.Assert(diagnosticsContext != null);

            if (!document.TryGetValue(Constants.EncryptedInfo, out CosmosElement encryptedInfo))
            {
                return document;
            }

            EncryptionProperties encryptionProperties = JsonConvert.DeserializeObject<EncryptionProperties>(encryptedInfo.ToString());

            JObject plainTextJObj = await this.DecryptContentAsync(
                encryptionProperties,
                diagnosticsContext,
                cancellationToken);

            Dictionary<string, CosmosElement> documentContent = document.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            documentContent.Remove(Constants.EncryptedInfo);

            List<string> decryptedPaths = new List<string>();
            foreach (JProperty property in plainTextJObj.Properties())
            {
                documentContent.Add(property.Name, property.Value.ToObject<CosmosElement>());
                decryptedPaths.Add("/" + property.Name);
            }

            this.DecryptedPaths = decryptedPaths;
            return CosmosObject.Create(documentContent);
        }

        private async Task<JObject> DecryptContentAsync(
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (encryptionProperties.EncryptionFormatVersion != 2)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await this.Encryptor.DecryptAsync(
                encryptionProperties.EncryptedData,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            }

            JObject plainTextJObj;
            using (MemoryStream memoryStream = new MemoryStream(plainText))
            using (StreamReader streamReader = new StreamReader(memoryStream))
            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
            {
                plainTextJObj = JObject.Load(jsonTextReader);
            }

            return plainTextJObj;
        }        
    }
}