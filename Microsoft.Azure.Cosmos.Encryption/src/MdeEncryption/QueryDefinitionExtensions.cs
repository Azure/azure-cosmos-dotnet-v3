//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// This class provides extension methods for <see cref="QueryDefinition"/>.
    /// </summary>
    public static class QueryDefinitionExtensions
    {
        /// <summary>
        /// Gets a QueryDefinition with Encrypted Parameters.
        /// </summary>
        /// <param name="queryDefinition"> Query Definition to be replaced with Encrypted Values.</param>
        /// <param name="name"> Query Paramerter Name. </param>
        /// <param name="value"> Query Paramerter Value.</param>
        /// <param name="path"> Encrypted Property Path. </param>
        /// <param name="container"> Container handler </param>
        /// <typeparam name="T"> Type of item.</typeparam>
        /// <returns> QueryDefinition with encrypted parameters. </returns>
        public static async Task<QueryDefinition> AddEncryptedParameterAsync<T>(
            this QueryDefinition queryDefinition,
            string name,
            T value,
            string path,
            Container container)
        {
            if (queryDefinition == null)
            {
                throw new ArgumentNullException("Missing QueryDefinition in the argument");
            }

            if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
            {
                throw new InvalidOperationException($"Invalid path {path ?? string.Empty}, {nameof(path)}");
            }

            if (string.IsNullOrWhiteSpace(name) || value == null)
            {
                throw new ArgumentNullException("Name or Value argument is Null.");
            }

            QueryDefinition withEncryptedValues = queryDefinition;

            if (container != null && container is MdeContainer mdeContainer)
            {
                Stream itemStream = mdeContainer.CosmosSerializer.ToStream<T>(value);
                JToken propertyValueToEncrypt = EncryptionProcessor.BaseSerializer.FromStream<JToken>(itemStream);

                await mdeContainer.MdeEncryptionProcessor.InitializeMdeProcessorIfNotInitializedAsync();

                try
                {
                    // get the paths encryption setting.
                    MdeEncryptionSettings settings = await mdeContainer.MdeEncryptionProcessor.GetEncryptionSettingForPropertyAsync(path.Substring(1));

                    ClientEncryptionDataType? clientEncryptionDataType = null;

                    // use the configured data type for the path else identify and add the marker infront of the cipher text.
                    byte[] serializedData = settings.ClientEncryptionDataType != null
                        ? Serialize(propertyValueToEncrypt, settings.ClientEncryptionDataType)
                        : Serialize(propertyValueToEncrypt, clientEncryptionDataType = GetDataTypeForSerialization(propertyValueToEncrypt));
                    byte[] cipherText = settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(serializedData);

                    if (settings.ClientEncryptionDataType == null)
                    {
                        if (clientEncryptionDataType == null)
                        {
                            throw new InvalidOperationException($"Failed to Identify Data Type for Query Parameter: {name} with path: {path}");
                        }

                        byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                        cipherTextWithTypeMarker[0] = (byte)clientEncryptionDataType;
                        Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                        withEncryptedValues.WithParameter(name, cipherTextWithTypeMarker);
                    }
                    else
                    {
                        withEncryptedValues.WithParameter(name, cipherText);
                    }

                    return await Task.FromResult(withEncryptedValues);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Invalid Query Parameter passed: {ex.Message}");
                }
            }
            else
            {
                throw new ArgumentException("For executing queries on encrypted paths please configure Cosmos Client with Encryption Support");
            }
        }

        private static byte[] Serialize(JToken propertyValue, ClientEncryptionDataType? clientEncryptionDataType = null)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();
            SqlNvarcharSerializer sqlNvarcharSerializer = new SqlNvarcharSerializer(-1);

            return clientEncryptionDataType switch
            {
                ClientEncryptionDataType.Bool => sqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>()),
                ClientEncryptionDataType.Double => sqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>()),
                ClientEncryptionDataType.Long => sqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>()),
                ClientEncryptionDataType.String => sqlNvarcharSerializer.Serialize(propertyValue.ToObject<string>()),
                _ => throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {clientEncryptionDataType}"),
            };
        }

        private static ClientEncryptionDataType? GetDataTypeForSerialization(JToken propertyValueToEncrypt)
        {
            ClientEncryptionDataType? clientEncryptionDataType = null;
            switch (propertyValueToEncrypt.Type)
            {
                case JTokenType.Boolean:
                    clientEncryptionDataType = ClientEncryptionDataType.Bool;
                    break;
                case JTokenType.Float:
                    clientEncryptionDataType = ClientEncryptionDataType.Double;
                    break;
                case JTokenType.Integer:
                    clientEncryptionDataType = ClientEncryptionDataType.Long;
                    break;
                case JTokenType.String:
                    clientEncryptionDataType = ClientEncryptionDataType.String;
                    break;
            }

            return clientEncryptionDataType;
        }
    }
}
