//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A serializer implementation that allows for encryption of properties during serialization and decryption during deserialization.
    /// See https://tbd for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionSerializer : CosmosSerializer
    {
        private readonly CosmosSerializer baseSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());

        /// <summary>
        /// Gets the provider used to wrap and unwrap data encryption keys.
        /// </summary>
        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        /// <summary>
        /// Creates a new instance of a serializer that allows for encryption of properties during serialization and decryption during deserialization.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">Provider that can be used to wrap and unwrap data encryption keys for envelope based encryption.</param>
        public EncryptionSerializer(EncryptionKeyWrapProvider encryptionKeyWrapProvider)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
        }

        /// <summary>
        /// Converts the input object to a Stream and encrypts top level properties of the type T which have been annotated with the <see cref="CosmosEncryptAttribute"/>. 
        /// </summary>
        /// <param name="input">Any type passed to <see cref="Container"/>.</param>
        /// <param name="container">Container that the item is in.</param>
        /// <param name="requestOptions">Request options for the request.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of the operation.</param>
        /// <returns>A readable Stream containing JSON of the serialized object.</returns>
        // The caller will take ownership of the stream and ensure it is correctly disposed of.
        internal override async Task<Stream> ToStreamAsync<T>(T input, Container container, RequestOptions requestOptions, CancellationToken cancellationToken)
        {
            ContainerCore containerCore = (ContainerCore)container;
            ItemRequestOptions itemRequestOptions = requestOptions as ItemRequestOptions;

            if (itemRequestOptions == null || itemRequestOptions.EncryptionOptions == null || itemRequestOptions.EncryptionOptions.DataEncryptionKey == null)
            {
                return this.baseSerializer.ToStream<T>(input);
            }

            DataEncryptionKey dek = ((DatabaseCore)containerCore.Database).GetDataEncryptionKey(itemRequestOptions.EncryptionOptions.DataEncryptionKey.Id);

            DataEncryptionKeyCore dekCore = (DataEncryptionKeyInlineCore)dek;
            (DataEncryptionKeyProperties dekProperties, InMemoryRawDek inMemoryRawDek) = await dekCore.FetchUnwrappedAsync(cancellationToken);

            PropertyInfo[] typeProperties = typeof(T).GetProperties();

            JObject itemJObj = JObject.FromObject(input);

            JObject toEncryptJObj = new JObject();

            foreach (PropertyInfo typeProperty in typeProperties)
            {
                Attribute shouldEncrypt = Attribute.GetCustomAttribute(typeProperty, typeof(CosmosEncryptAttribute));
                if (shouldEncrypt != null)
                {
                    string propertyName = typeProperty.Name;
                    Attribute jsonProperty = Attribute.GetCustomAttribute(typeProperty, typeof(JsonPropertyAttribute));
                    if (jsonProperty != null && ((JsonPropertyAttribute)jsonProperty).PropertyName != null)
                    {
                        propertyName = ((JsonPropertyAttribute)jsonProperty).PropertyName;
                    }

                    JToken propertyValueHolder = itemJObj.Property(propertyName).Value;

                    // Even null in the JSON is a JToken with Type Null, this null check is just a sanity check
                    if (propertyValueHolder != null)
                    {
                        toEncryptJObj.Add(propertyName, itemJObj.Property(propertyName).Value.Value<JToken>());
                        itemJObj.Remove(propertyName);
                    }
                }
            }

            if (!toEncryptJObj.HasValues)
            {
                return this.baseSerializer.ToStream(input);
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

        /// <inheritdoc />
        internal override async Task<T> FromStreamAsync<T>(Stream stream, Container container, RequestOptions requestOptions, CancellationToken cancellationToken)
        {
            // Container is null for stored procedure response
            if (container == null)
            {
                return this.baseSerializer.FromStream<T>(stream);
            }

            JObject itemJObj = null;
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
            {
                itemJObj = JObject.Load(jsonTextReader);
            }

            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.Properties.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            if (encryptionPropertiesJObj == null)
            {
                return itemJObj.ToObject<T>();
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
            if (encryptionProperties.EncryptionFormatVersion != 1)
            {
                throw new CosmosException(HttpStatusCode.InternalServerError, $"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            ContainerCore containerCore = (ContainerCore)container;
            DataEncryptionKeyCore tempDek = (DataEncryptionKeyInlineCore)((DatabaseCore)containerCore.Database).GetDataEncryptionKey(id: "unknown");
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
            return itemJObj.ToObject<T>();
        }

        /// <inheritdoc/>
        public override T FromStream<T>(Stream stream)
        {
            // Will be called in query response paths
            return this.baseSerializer.FromStream<T>(stream);
        }

        /// <inheritdoc/>
        public override Stream ToStream<T>(T input)
        {
            // Will be called in parameterized query request paths
            return this.baseSerializer.ToStream<T>(input);
        }
    }
}
