//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using global::Azure.Core.Cryptography;

    /// <summary>
    /// Names of well-known implementations of <see cref="IKeyEncryptionKeyResolver" />.
    /// </summary>
    /// <remarks>
    /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
    /// </remarks>
    public static class KeyEncryptionKeyResolverName
    {
        /// <summary>
        /// Name of the <see cref="IKeyEncryptionKeyResolver" /> implementation for key encryption keys in Azure Key Vault.
        /// </summary>
        public const string AzureKeyVault = "AZURE_KEY_VAULT";
    }
}
