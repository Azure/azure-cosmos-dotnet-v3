//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// The purpose/intention to introduce this class is to utilize the cache provide by the <see cref="EncryptionKeyStoreProvider"/> abstract class. This class basically
    /// redirects all the corresponding calls to <see cref="EncryptionKeyWrapProvider"/> 's overridden methods and thus allowing us
    /// to utilize the virtual method <see cref="EncryptionKeyStoreProvider.GetOrCreateDataEncryptionKey"/> to access the cache.
    ///
    /// Note: Since <see cref="EncryptionKeyStoreProvider.Sign"/> and <see cref="EncryptionKeyStoreProvider.Verify"/> methods are not exposed, <see cref="EncryptionKeyStoreProvider.GetOrCreateSignatureVerificationResult"/> is not supported either.
    ///
    /// <remark>
    /// The call hierarchy is as follows. Note, all core MDE API's used in internal cosmos encryption code are passed an EncryptionKeyStoreProviderImpl object.
    /// ProtectedDataEncryptionKey -> KeyEncryptionKey(containing EncryptionKeyStoreProviderImpl object) -> EncryptionKeyStoreProviderImpl.WrapKey -> this.EncryptionKeyWrapProvider.WrapKeyAsync
    /// ProtectedDataEncryptionKey -> KeyEncryptionKey(containing EncryptionKeyStoreProviderImpl object) -> EncryptionKeyStoreProviderImpl.UnWrapKey -> this.EncryptionKeyWrapProvider.UnwrapKeyAsync 
    /// </remark>
    /// </summary>
    internal class EncryptionKeyStoreProviderImpl : EncryptionKeyStoreProvider
    {
        private readonly EncryptionKeyWrapProvider encryptionKeyWrapProvider;

        public EncryptionKeyStoreProviderImpl(EncryptionKeyWrapProvider encryptionKeyWrapProvider)
        {
            this.encryptionKeyWrapProvider = encryptionKeyWrapProvider;
        }

        public override string ProviderName => this.encryptionKeyWrapProvider.ProviderName;

        public override byte[] UnwrapKey(string encryptionKeyId, Data.Encryption.Cryptography.KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
        {
            // since AzureKeyVaultKeyWrapProvider/AzureKeyVaultKeyStoreProvider has access to KeyStoreProvider cache lets directly call UnwrapKeyAsync instead of looking up cache ourselves,
            // avoids additional cache look up by AzureKeyVaultKeyStoreProvider Unwrap() in case of cache miss
            if (this.encryptionKeyWrapProvider is AzureKeyVaultKeyWrapProvider)
            {
                return this.encryptionKeyWrapProvider.UnwrapKeyAsync(encryptionKeyId, algorithm.ToString(), encryptedKey)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                // for any other custom derived instances of EncryptionKeyWrapProvider, since we do not expose GetOrCreateDataEncryptionKey we first look up the cache.
                // Cache miss results in call to UnWrapCore which updates the cache after UnwrapKeyAsync is called.
                return this.GetOrCreateDataEncryptionKey(encryptedKey.ToHexString(), UnWrapKeyCore);

                // delegate that is called by GetOrCreateDataEncryptionKey, which unwraps the key and updates the cache in case of cache miss.
                byte[] UnWrapKeyCore()
                {
                    return this.encryptionKeyWrapProvider.UnwrapKeyAsync(encryptionKeyId, algorithm.ToString(), encryptedKey)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
            }
        }

        public override byte[] WrapKey(string encryptionKeyId, Data.Encryption.Cryptography.KeyEncryptionKeyAlgorithm algorithm, byte[] key)
        {
            return this.encryptionKeyWrapProvider.WrapKeyAsync(encryptionKeyId, algorithm.ToString(), key)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            throw new NotSupportedException("The Sign operation is not supported. ");
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotSupportedException("The Verify operation is not supported. ");
        }
    }
}
