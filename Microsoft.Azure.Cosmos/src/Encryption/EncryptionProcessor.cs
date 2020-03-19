//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class EncryptionProcessor
    {
        private static readonly CosmosSerializer baseSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());

        public async Task<Stream> EncryptAsync(
            Stream input,
            EncryptionOptions encryptionOptions,
            DatabaseCore database,
            DataEncryptionKeyProvider dataEncryptionKeyProvider,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(encryptionOptions != null);
            Debug.Assert(database != null);
            Debug.Assert(diagnosticsContext != null);

            if (dataEncryptionKeyProvider == null)
            {
                throw new ArgumentException(ClientResources.DataEncryptionKeyProviderNotConfigured);
            }

            if (string.IsNullOrEmpty(encryptionOptions.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.DataEncryptionKeyId));
            }

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
            }

            if (encryptionOptions.EncryptionAlgorithm != CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED)
            {
                throw new ArgumentException(
                    $"Unknown encryption algorithm {encryptionOptions.EncryptionAlgorithm.ToString()}",
                    nameof(encryptionOptions.EncryptionAlgorithm));
            }

            if (encryptionOptions.PathsToEncrypt.Count == 0)
            {
                return input;
            }

            foreach (string path in encryptionOptions.PathsToEncrypt)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
                {
                    throw new ArgumentException($"Invalid path {path ?? string.Empty}", nameof(encryptionOptions.PathsToEncrypt));
                }
            }

            byte[] dataEncryptionKey = await dataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, cancellationToken);
            EncryptionAlgorithm algorithm = EncryptionProcessor.GetEncryptionAlgorithm(dataEncryptionKey, encryptionOptions.EncryptionAlgorithm);

            JObject itemJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(input);

            JObject toEncryptJObj = new JObject();

            foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
            {
                string propertyName = pathToEncrypt.Substring(1);
                JToken propertyValueHolder = itemJObj.Property(propertyName).Value;

                // Even null in the JSON is a JToken with Type Null, this null check is just a sanity check
                if (propertyValueHolder != null)
                {
                    toEncryptJObj.Add(propertyName, itemJObj.Property(propertyName).Value.Value<JToken>());
                    itemJObj.Remove(propertyName);
                }
            }

            MemoryStream memoryStream = EncryptionProcessor.baseSerializer.ToStream<JObject>(toEncryptJObj) as MemoryStream;
            Debug.Assert(memoryStream != null);
            Debug.Assert(memoryStream.TryGetBuffer(out _));

            byte[] plainText = memoryStream.GetBuffer();

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                dataEncryptionKeyId: encryptionOptions.DataEncryptionKeyId,
                encryptionFormatVersion: 2,
                encryptionAlgorithmId: encryptionOptions.EncryptionAlgorithm,
                encryptedData: algorithm.EncryptData(plainText));

            itemJObj.Add(Constants.Properties.EncryptedInfo, JObject.FromObject(encryptionProperties));
            return EncryptionProcessor.baseSerializer.ToStream(itemJObj);
        }

        public async Task<Stream> DecryptAsync(
            Stream input,
            DatabaseCore database,
            DataEncryptionKeyProvider dataEncryptionKeyProvider,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(database != null);
            Debug.Assert(dataEncryptionKeyProvider != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
                }
            }

            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.Properties.EncryptedInfo);
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
            if (encryptionProperties.EncryptionFormatVersion != 2)
            {
                throw CosmosExceptionFactory.CreateInternalServerErrorException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] dataEncryptionKey = await dataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, cancellationToken);
            EncryptionAlgorithm algorithm = EncryptionProcessor.GetEncryptionAlgorithm(dataEncryptionKey, encryptionProperties.EncryptionAlgorithmId);

            byte[] plainText = algorithm.DecryptData(encryptionProperties.EncryptedData);

            JObject plainTextJObj = null;
            using (MemoryStream memoryStream = new MemoryStream(plainText))
            using (StreamReader streamReader = new StreamReader(memoryStream))
            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
            {
                plainTextJObj = JObject.Load(jsonTextReader);
            }

            foreach (JProperty property in plainTextJObj.Properties())
            {
                itemJObj.Add(property.Name, property.Value);
            }

            itemJObj.Remove(Constants.Properties.EncryptedInfo);
            return EncryptionProcessor.baseSerializer.ToStream(itemJObj);
        }

        private static EncryptionAlgorithm GetEncryptionAlgorithm(
            byte[] dataEncryptionKey,
            CosmosEncryptionAlgorithm encryptionAlgorithmId)
        {
            Debug.Assert(encryptionAlgorithmId == CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED);
            return new AeadAes256CbcHmac256Algorithm(
                new AeadAes256CbcHmac256EncryptionKey(dataEncryptionKey, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant),
                EncryptionType.Randomized,
                algorithmVersion: 1);
        }
    }
}
