//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using global::Azure.Core.Cryptography;

    /// <summary>
    /// Has constants for names of well-known implementations of <see cref="IKeyEncryptionKeyResolver" />.
    /// </summary>
    public static class KeyEncryptionKeyResolverName
    {
        /// <summary>
        /// IKeyEncryptionKeyResolver implementation for keys in Azure Key Vault.
        /// </summary>
        public const string AzureKeyVault = "AZURE_KEY_VAULT";
    }
}
