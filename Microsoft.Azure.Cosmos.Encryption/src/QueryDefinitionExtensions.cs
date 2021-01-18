//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;
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
        /// <typeparam name="T"> Type of item.</typeparam>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns> QueryDefinition with encrypted parameters. </returns>
        /// <example>
        /// This example shows how to pass in a QueryDefinition with Encryption support to AddParameterAsync
        /// to encrypt the required Property for running Query on encrypted data.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// containerWithEncryption = await this.cosmosDatabase.GetContainer("id").InitializeEncryptionAsync();
        /// QueryDefinition withEncryptedParameter = containerWithEncryption.CreateQueryDefinition(
        ///     "SELECT * FROM c where c.PropertyName = @PropertyValue");
        /// await withEncryptedParameter.AddParameterAsync(
        ///     "@PropertyName",
        ///     PropertyValue,
        ///     "/PropertyName");
        /// ]]>
        /// </code>
        /// </example>
        public static async Task<QueryDefinition> AddParameterAsync<T>(
            this QueryDefinition queryDefinition,
            string name,
            T value,
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (queryDefinition == null)
            {
                throw new ArgumentNullException(nameof(queryDefinition));
            }

            if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
            {
                throw new InvalidOperationException($"Invalid path {path ?? string.Empty}, {nameof(path)}. ");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("Name cannot be Null or Empty. ");
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            QueryDefinition withEncryptedValues = queryDefinition;

            if (queryDefinition is EncryptionQueryDefinition encryptionQueryDefinition)
            {
                MdeContainer mdeContainer = (MdeContainer)encryptionQueryDefinition.Container;
                Stream valueStream = mdeContainer.CosmosSerializer.ToStream<T>(value);
                JToken propertyValueToEncrypt = MdeEncryptionProcessor.BaseSerializer.FromStream<JToken>(valueStream);

                // not really required, but will have things setup for subsequent queries or operations on this Container.
                await mdeContainer.MdeEncryptionProcessor.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

                // get the path's encryption setting.
                MdeEncryptionSettings settings = await mdeContainer.MdeEncryptionProcessor.MdeEncryptionSettings.GetEncryptionSettingForPropertyAsync(
                    path.Substring(1),
                    mdeContainer.MdeEncryptionProcessor,
                    cancellationToken);

                if (settings == null)
                {
                    // property not encrypted.
                    withEncryptedValues.WithParameter(name, value);
                    return withEncryptedValues;
                }

                if (settings.EncryptionType == EncryptionType.Randomized)
                {
                    throw new ArgumentException($"Unsupported argument with Path: {path} for query. For executing queries on encrypted path requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                }

                (MdeEncryptionProcessor.TypeMarker typeMarker, byte[] serializedData) = MdeEncryptionProcessor.Serialize(propertyValueToEncrypt);

                byte[] cipherText = settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(serializedData);

                if (cipherText == null)
                {
                    throw new InvalidOperationException($"{nameof(AddParameterAsync)} returned null cipherText from {nameof(settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                }

                byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                cipherTextWithTypeMarker[0] = (byte)typeMarker;
                Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                withEncryptedValues.WithParameter(name, cipherTextWithTypeMarker);

                return withEncryptedValues;
            }
            else
            {
                throw new ArgumentException("Executing queries on encrypted path requires the use of an encryption - enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }
        }
    }
}
