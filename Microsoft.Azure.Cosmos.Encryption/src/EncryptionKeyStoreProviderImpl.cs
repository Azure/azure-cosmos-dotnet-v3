//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using global::Azure.Core.Cryptography;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// The purpose/intention to introduce this class is to utilize the cache provide by the <see cref="EncryptionKeyStoreProvider"/> abstract class. This class basically
    /// redirects all the corresponding calls to <see cref="IKeyEncryptionKeyResolver"/> 's methods and thus allowing us
    /// to utilize the virtual method <see cref="EncryptionKeyStoreProvider.GetOrCreateDataEncryptionKey"/> to access the cache.
    ///
    /// Note: Since <see cref="EncryptionKeyStoreProvider.Sign"/> and <see cref="EncryptionKeyStoreProvider.Verify"/> methods are not exposed, <see cref="EncryptionKeyStoreProvider.GetOrCreateSignatureVerificationResult"/> is not supported either.
    ///
    /// <remark>
    /// The call hierarchy is as follows. Note, all core MDE API's used in internal cosmos encryption code are passed an EncryptionKeyStoreProviderImpl object.
    /// ProtectedDataEncryptionKey -> KeyEncryptionKey(containing EncryptionKeyStoreProviderImpl object) -> EncryptionKeyStoreProviderImpl.WrapKey -> this.keyEncryptionKeyResolver.WrapKey
    /// ProtectedDataEncryptionKey -> KeyEncryptionKey(containing EncryptionKeyStoreProviderImpl object) -> EncryptionKeyStoreProviderImpl.UnWrapKey -> this.keyEncryptionKeyResolver.UnwrapKey
    /// </remark>
    /// </summary>
    internal class EncryptionKeyStoreProviderImpl : EncryptionKeyStoreProvider
    {
        public const string RsaOaepWrapAlgorithm = "RSA-OAEP";

        private readonly IKeyEncryptionKeyResolver keyEncryptionKeyResolver;

        public EncryptionKeyStoreProviderImpl(IKeyEncryptionKeyResolver keyEncryptionKeyResolver, string providerName)
        {
            this.keyEncryptionKeyResolver = keyEncryptionKeyResolver;
            this.ProviderName = providerName;
            this.DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero;
        }

        public override string ProviderName { get; }

        public override byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
        {
            // since we do not expose GetOrCreateDataEncryptionKey we first look up the cache.
            // Cache miss results in call to UnWrapCore which updates the cache after UnwrapKeyAsync is called.
            return this.GetOrCreateDataEncryptionKey(encryptedKey.ToHexString(), UnWrapKeyCore);

            // delegate that is called by GetOrCreateDataEncryptionKey, which unwraps the key and updates the cache in case of cache miss.
            byte[] UnWrapKeyCore()
            {
                return this.keyEncryptionKeyResolver
                    .Resolve(encryptionKeyId)
                    .UnwrapKey(EncryptionKeyStoreProviderImpl.GetNameForKeyEncryptionKeyAlgorithm(algorithm), encryptedKey);
            }
        }

        public override byte[] WrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] key)
        {
            return this.keyEncryptionKeyResolver
                .Resolve(encryptionKeyId)
                .WrapKey(EncryptionKeyStoreProviderImpl.GetNameForKeyEncryptionKeyAlgorithm(algorithm), key);
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            throw new NotSupportedException("The Sign operation is not supported.");
        }

        /// <Remark>
        /// The public facing Cosmos Encryption library interface does not expose this method, hence not supported.
        /// </Remark>
        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotSupportedException("The Verify operation is not supported.");
        }

        private static string GetNameForKeyEncryptionKeyAlgorithm(KeyEncryptionKeyAlgorithm algorithm)
        {
            if (algorithm == KeyEncryptionKeyAlgorithm.RSA_OAEP)
            {
                return EncryptionKeyStoreProviderImpl.RsaOaepWrapAlgorithm;
            }

            throw new InvalidOperationException(string.Format("Unexpected algorithm {0}", algorithm));
        }
    }
}
