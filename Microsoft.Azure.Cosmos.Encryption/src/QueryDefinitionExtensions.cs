//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
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
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns> QueryDefinition with encrypted parameters. </returns>
        public static async Task<QueryDefinition> AddEncryptedParameterAsync<T>(
            this QueryDefinition queryDefinition,
            string name,
            T value,
            string path,
            Container container,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                Stream valueStream = mdeContainer.CosmosSerializer.ToStream<T>(value);
                JToken propertyValueToEncrypt = MdeEncryptionProcessor.BaseSerializer.FromStream<JToken>(valueStream);

                // not really required, but will have things setup for subsequent queries or operations on this Container.
                await mdeContainer.MdeEncryptionProcessor.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

                // get the path's encryption setting.
                MdeEncryptionSettings settings = await mdeContainer.MdeEncryptionProcessor.MdeEncryptionSettings.GetorUpdateEncryptionSettingForPropertyAsync(
                    path.Substring(1),
                    mdeContainer.MdeEncryptionProcessor,
                    cancellationToken);

                if (settings == null)
                {
                    // property not encrypted.
                    withEncryptedValues.WithParameter(name, value);
                    return withEncryptedValues;
                }

                (MdeEncryptionProcessor.TypeMarker typeMarker, byte[] serializedData) = MdeEncryptionProcessor.Serialize(propertyValueToEncrypt);

                byte[] cipherText = settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(serializedData);

                if (cipherText == null)
                {
                    throw new InvalidOperationException($"{nameof(AddEncryptedParameterAsync)} returned null cipherText from {nameof(settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}.");
                }

                byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                cipherTextWithTypeMarker[0] = (byte)typeMarker;
                Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                withEncryptedValues.WithParameter(name, cipherTextWithTypeMarker);

                return withEncryptedValues;
            }
            else
            {
                throw new ArgumentException("For executing queries on encrypted paths please configure Cosmos Client with Encryption Support");
            }
        }
    }
}
