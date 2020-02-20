//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
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
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(encryptionOptions != null);
            Debug.Assert(database != null);
            Debug.Assert(diagnosticsContext != null);

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
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

            if (encryptionOptions.DataEncryptionKey == null)
            {
                throw new ArgumentException("Invalid encryption options", nameof(encryptionOptions.DataEncryptionKey));
            }

            if (encryptionKeyWrapProvider == null)
            {
                throw new ArgumentException(ClientResources.EncryptionKeyWrapProviderNotConfigured);
            }

            DataEncryptionKey dek = database.GetDataEncryptionKey(encryptionOptions.DataEncryptionKey.Id);

            DataEncryptionKeyCore dekCore = (DataEncryptionKeyInlineCore)dek;
            (DataEncryptionKeyProperties dekProperties, InMemoryRawDek inMemoryRawDek) = await dekCore.FetchUnwrappedAsync(
                diagnosticsContext,
                cancellationToken);

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
                dataEncryptionKeyRid: dekProperties.ResourceId,
                encryptionFormatVersion: 1,
                encryptedData: inMemoryRawDek.AlgorithmUsingRawDek.EncryptData(plainText));

            itemJObj.Add(Constants.Properties.EncryptedInfo, JObject.FromObject(encryptionProperties));
            return EncryptionProcessor.baseSerializer.ToStream(itemJObj);
        }

        public async Task<Stream> DecryptAsync(
            Stream input,
            DatabaseCore database,
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(database != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(diagnosticsContext != null);

            if (encryptionKeyWrapProvider == null)
            {
                return input;
            }

            JObject itemJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(input);

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
            if (encryptionProperties.EncryptionFormatVersion != 1)
            {
                throw new CosmosException(HttpStatusCode.InternalServerError, $"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            DataEncryptionKeyCore tempDek = (DataEncryptionKeyInlineCore)database.GetDataEncryptionKey(id: "unknown");
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await tempDek.FetchUnwrappedByRidAsync(
                encryptionProperties.DataEncryptionKeyRid,
                diagnosticsContext,
                cancellationToken);

            byte[] plainText = inMemoryRawDek.AlgorithmUsingRawDek.DecryptData(encryptionProperties.EncryptedData);

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
    }
}
