//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Text;

    /// <summary>
    /// Encryption key class containing 4 keys. This class is used by AeadAes256CbcHmac256Algorithm
    /// 1) root key - Main key that is used to derive the keys used in the encryption algorithm
    /// 2) encryption key - A derived key that is used to encrypt the plain text and generate cipher text
    /// 3) mac_key - A derived key that is used to compute HMAC of the cipher text
    /// 4) iv_key - A derived key that is used to generate a synthetic IV from plain text data.
    /// </summary>
    internal sealed class AeadAes256CbcHmac256EncryptionKey : SymmetricKey
    {
        /// <summary>
        /// Key size in bits.
        /// </summary>
        internal const int KeySize = 256;

        /// <summary>
        /// Encryption Key Salt format. This is used to derive the encryption key from the root key.
        /// </summary>
        private const string EncryptionKeySaltFormat = @"Microsoft Azure Cosmos DB encryption key with encryption algorithm:{0} and key length:{1}";

        /// <summary>
        /// MAC Key Salt format. This is used to derive the MAC key from the root key.
        /// </summary>
        private const string MacKeySaltFormat = @"Microsoft Azure Cosmos DB MAC key with encryption algorithm:{0} and key length:{1}";

        /// <summary>
        /// IV Key Salt format. This is used to derive the IV key from the root key. This is only used for Deterministic encryption.
        /// </summary>
        private const string IvKeySaltFormat = @"Microsoft Azure Cosmos DB IV key with encryption algorithm:{0} and key length:{1}";

        /// <summary>
        /// Encryption Key.
        /// </summary>
        private readonly SymmetricKey encryptionKey;

        /// <summary>
        /// MAC key.
        /// </summary>
        private readonly SymmetricKey macKey;

        /// <summary>
        /// IV Key.
        /// </summary>
        private readonly SymmetricKey ivKey;

        /// <summary>
        /// The name of the algorithm this key will be used with.
        /// </summary>
        private readonly string algorithmName;

        /// <summary>
        /// Derives all the required keys from the given root key
        /// </summary>
        internal AeadAes256CbcHmac256EncryptionKey(byte[] rootKey, string algorithmName)
            : base(rootKey)
        {
            this.algorithmName = algorithmName;

            int keySizeInBytes = KeySize / 8;

            // Key validation
            if (rootKey.Length != keySizeInBytes)
            {
                throw EncryptionExceptionFactory.InvalidKeySize(
                    this.algorithmName,
                    rootKey.Length,
                    keySizeInBytes);
            }

            // Derive keys from the root key
            //
            // Derive encryption key
            string encryptionKeySalt = string.Format(
                EncryptionKeySaltFormat,
                this.algorithmName,
                KeySize);
            byte[] buff1 = new byte[keySizeInBytes];
            SecurityUtility.GetHMACWithSHA256(Encoding.Unicode.GetBytes(encryptionKeySalt), this.RootKey, buff1);
            this.encryptionKey = new SymmetricKey(buff1);

            // Derive mac key
            string macKeySalt = string.Format(MacKeySaltFormat, this.algorithmName, KeySize);
            byte[] buff2 = new byte[keySizeInBytes];
            SecurityUtility.GetHMACWithSHA256(Encoding.Unicode.GetBytes(macKeySalt), this.RootKey, buff2);
            this.macKey = new SymmetricKey(buff2);

            // Derive iv key
            string ivKeySalt = string.Format(IvKeySaltFormat, this.algorithmName, KeySize);
            byte[] buff3 = new byte[keySizeInBytes];
            SecurityUtility.GetHMACWithSHA256(Encoding.Unicode.GetBytes(ivKeySalt), this.RootKey, buff3);
            this.ivKey = new SymmetricKey(buff3);
        }

        /// <summary>
        /// Gets Encryption key that should be used for encryption and decryption.
        /// </summary>
        internal byte[] EncryptionKey => this.encryptionKey.RootKey;

        /// <summary>
        /// Gets MAC key should be used to compute and validate HMAC.
        /// </summary>
        internal byte[] MACKey => this.macKey.RootKey;

        /// <summary>
        /// Gets IV key should be used to compute synthetic IV from a given plain text.
        /// </summary>
        internal byte[] IVKey => this.ivKey.RootKey;
    }
}
