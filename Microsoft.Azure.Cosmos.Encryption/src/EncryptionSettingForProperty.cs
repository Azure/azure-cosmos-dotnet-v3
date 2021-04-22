//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettingForProperty
    {
        public string ClientEncryptionKeyId { get; }

        public EncryptionType EncryptionType { get; }

        public EncryptionProcessor EncryptionProcessor { get; }

        public EncryptionSettingForProperty(string clientEncryptionKeyId, EncryptionType encryptionType, EncryptionProcessor encryptionProcessor)
        {
            this.ClientEncryptionKeyId = clientEncryptionKeyId ?? throw new ArgumentNullException(nameof(clientEncryptionKeyId));
            this.EncryptionType = encryptionType;
            this.EncryptionProcessor = encryptionProcessor ?? throw new ArgumentNullException(nameof(encryptionProcessor));
        }

        internal async Task<AeadAes256CbcHmac256EncryptionAlgorithm> BuildEncryptionAlgorithmForSettingAsync(CancellationToken cancellationToken)
        {
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await this.EncryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId: this.ClientEncryptionKeyId,
                    container: this.EncryptionProcessor.Container,
                    cancellationToken: cancellationToken,
                    shouldForceRefresh: false);

            ProtectedDataEncryptionKey protectedDataEncryptionKey;

            try
            {
                // we pull out the Encrypted Data Encryption Key and build the Protected Data Encryption key
                // Here a request is sent out to unwrap using the Master Key configured via the Key Encryption Key.
                protectedDataEncryptionKey = this.BuildProtectedDataEncryptionKey(
                    clientEncryptionKeyProperties,
                    this.EncryptionProcessor.EncryptionKeyStoreProvider,
                    this.ClientEncryptionKeyId);
            }
            catch (RequestFailedException ex)
            {
                // The access to master key was probably revoked. Try to fetch the latest ClientEncryptionKeyProperties from the backend.
                // This will succeed provided the user has rewraped the Client Encryption Key with right set of meta data.
                // This is based on the AKV provider implementaion so we expect a RequestFailedException in case other providers are used in unwrap implementation.
                if (ex.Status == (int)HttpStatusCode.Forbidden)
                {
                    clientEncryptionKeyProperties = await this.EncryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                        clientEncryptionKeyId: this.ClientEncryptionKeyId,
                        container: this.EncryptionProcessor.Container,
                        cancellationToken: cancellationToken,
                        shouldForceRefresh: true);

                    // just bail out if this fails.
                    protectedDataEncryptionKey = this.BuildProtectedDataEncryptionKey(
                        clientEncryptionKeyProperties,
                        this.EncryptionProcessor.EncryptionKeyStoreProvider,
                        this.ClientEncryptionKeyId);
                }
                else
                {
                    throw;
                }
            }

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = new AeadAes256CbcHmac256EncryptionAlgorithm(
                   protectedDataEncryptionKey,
                   this.EncryptionType);

            return aeadAes256CbcHmac256EncryptionAlgorithm;
        }

        internal ProtectedDataEncryptionKey BuildProtectedDataEncryptionKey(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            string keyId)
        {
            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
               encryptionKeyStoreProvider);

            ProtectedDataEncryptionKey protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                   keyId,
                   keyEncryptionKey,
                   clientEncryptionKeyProperties.WrappedDataEncryptionKey);

            return protectedDataEncryptionKey;
        }
    }
}