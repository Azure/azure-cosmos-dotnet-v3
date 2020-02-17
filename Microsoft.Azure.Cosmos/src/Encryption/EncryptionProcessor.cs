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
    using Microsoft.Azure.Cosmos.Core;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class EncryptionProcessor
    {
        private readonly CosmosSerializer baseSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());

        public async Task<Stream> EncryptAsync(
            Stream input,
            EncryptionOptions encryptionOptions,
            ContainerCore container,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(encryptionOptions != null);
            Debug.Assert(container != null);

            if (encryptionOptions.EncryptedPaths == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.EncryptedPaths));
            }

            if (encryptionOptions.EncryptedPaths.Count == 0)
            {
                return input;
            }

            foreach (string path in encryptionOptions.EncryptedPaths)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
                {
                    throw new ArgumentException($"Invalid path {path ?? string.Empty}", nameof(encryptionOptions.EncryptedPaths));
                }
            }

            if (encryptionOptions.DataEncryptionKey == null)
            {
                throw new ArgumentException("Invalid encryption options", nameof(encryptionOptions.DataEncryptionKey));
            }

            if (container.ClientContext.ClientOptions.EncryptionSettings == null)
            {
                throw new ArgumentException(ClientResources.EncryptionSettingsNotConfigured);
            }

            DataEncryptionKey dek = ((DatabaseCore)container.Database).GetDataEncryptionKey(encryptionOptions.DataEncryptionKey.Id);

            DataEncryptionKeyCore dekCore = (DataEncryptionKeyInlineCore)dek;
            (DataEncryptionKeyProperties dekProperties, InMemoryRawDek inMemoryRawDek) = await dekCore.FetchUnwrappedAsync(cancellationToken);

            JObject itemJObj = this.baseSerializer.FromStream<JObject>(input);

            JObject toEncryptJObj = new JObject();

            foreach (string pathToEncrypt in encryptionOptions.EncryptedPaths)
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

            byte[] plainText = Encoding.UTF8.GetBytes(toEncryptJObj.ToString(Formatting.None));

            Debug.Assert(inMemoryRawDek.AlgorithmUsingRawDek.AlgorithmName == AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
            EncryptionProperties encryptionProperties = new EncryptionProperties(
                dataEncryptionKeyRid: dekProperties.ResourceId,
                encryptionFormatVersion: 1,
                encryptedData: inMemoryRawDek.AlgorithmUsingRawDek.EncryptData(plainText));

            itemJObj.Add(Constants.Properties.EncryptedInfo, JObject.FromObject(encryptionProperties));
            return this.baseSerializer.ToStream(itemJObj);
        }

        public async Task<Stream> DecryptAsync(
            Stream input,
            ContainerCore container,
            CancellationToken cancellationToken)
        {
            Contract.Assert(input != null);
            Debug.Assert(container != null);
            Debug.Assert(input.CanSeek);

            if (container.ClientContext.ClientOptions.EncryptionSettings == null)
            {
                return input;
            }

            if (container.ClientContext.ClientOptions.EncryptionSettings == null)
            {
                throw new ArgumentException(ClientResources.EncryptionSettingsNotConfigured);
            }

            JObject itemJObj = this.baseSerializer.FromStream<JObject>(input);

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

            DataEncryptionKeyCore tempDek = (DataEncryptionKeyInlineCore)((DatabaseCore)container.Database).GetDataEncryptionKey(id: "unknown");
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await tempDek.FetchUnwrappedByRidAsync(encryptionProperties.DataEncryptionKeyRid, cancellationToken);
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
            return this.baseSerializer.ToStream(itemJObj);
        }
    }
}
