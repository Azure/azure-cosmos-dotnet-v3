//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Data.AAP_PH.Cryptography;

    /// <summary>
    /// Implements Core wrapping and unwrapping methods that uses the
    /// <see cref="AapKeyVaultServices"/> AAP MasterKey.
    /// </summary>
    internal sealed class AapKeyVaultServices
    {
        private readonly EncryptionKeyStoreProvider encryptionKeyStoreProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AapKeyVaultServices"/> class.
        /// </summary>
        /// <param name="encryptionKeyStoreProvider"> Key Store Provider Service for Encrypting and Decrypting the Data Encryption Key </param>
        public AapKeyVaultServices(EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.encryptionKeyStoreProvider = encryptionKeyStoreProvider;
        }

        /// <summary>
        /// Unwrap the encrypted Key.
        /// </summary>
        /// <param name="wrappedKey">encrypted bytes.</param>
        /// <param name="name"> key name.</param>
        /// <param name="path"> key path.</param>
        /// <returns> Decrypted Data Encryption Key </returns>
        internal byte[] UnwrapKey(
            byte[] wrappedKey,
            string name,
            string path)
        {
            MasterKey masterKey = new MasterKey(name, path, this.encryptionKeyStoreProvider);
            return masterKey.DecryptEncryptionKey(wrappedKey);
        }

        /// <summary>
        /// Wrap the Key with latest Key version.
        /// </summary>
        /// <param name="key">plain text key.</param>
        /// <param name="name"> key name.</param>
        /// <param name="path"> key path.</param>
        /// <returns>Encrypted Data Encryption Key </returns>
        internal byte[] WrapKey(
            byte[] key,
            string name,
            string path)
        {
            MasterKey masterKey = new MasterKey(name, path, this.encryptionKeyStoreProvider);
            return masterKey.EncryptEncryptionKey(key);
        }
    }
}