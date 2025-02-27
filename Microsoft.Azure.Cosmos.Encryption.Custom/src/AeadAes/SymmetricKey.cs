//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Base class containing raw key bytes for symmetric key algorithms. Some encryption algorithms can use the key directly while others derive sub keys from this.
    /// If an algorithm needs to derive more keys, have a derived class from this and use it in the corresponding encryption algorithm.
    /// </summary>
    internal class SymmetricKey
    {
        /// <summary>
        /// The underlying key material
        /// </summary>
        private readonly byte[] rootKey;

        /// <summary>
        /// Constructor that initializes the root key.
        /// </summary>
        /// <param name="rootKey">root key</param>
        internal SymmetricKey(byte[] rootKey)
        {
            // Key validation
            if (rootKey == null || rootKey.Length == 0)
            {
                throw new ArgumentNullException(nameof(rootKey));
            }

            this.rootKey = rootKey;
        }

        /// <summary>
        /// Gets a copy of the plain text key
        /// This is needed for actual encryption/decryption.
        /// </summary>
        internal virtual byte[] RootKey => this.rootKey;

        /// <summary>
        /// Computes SHA256 value of the plain text key bytes
        /// </summary>
        /// <returns>A string containing SHA256 hash of the root key</returns>
        internal virtual string GetKeyHash()
        {
            return SecurityUtility.GetSHA256Hash(this.RootKey);
        }

        /// <summary>
        /// Gets the length of the root key
        /// </summary>
        /// <returns>
        /// Returns the length of the root key
        /// </returns>
        internal virtual int Length()
        {
            return this.rootKey.Length;
        }
    }
}
