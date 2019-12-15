//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;

    /// <summary>
    /// Interacts with Azure Key Vault to wrap (encrypt) and unwrap (decrypt) keys for envelope based encryption.
    /// See <see href="tbd"/> for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public class AzureKeyVaultKeyWrapProvider : KeyWrapProvider
    {
        private string clientId;

        private string clientSecret;

        /// <summary>
        /// Creates a new instance of a key wrap provider that interacts with Azure Key Vault.
        /// See <see href="https://docs.microsoft.com/en-us/azure/key-vault/key-vault-group-permissions-for-apps#applications" /> for details on
        /// Azure Key Vault authentication with applications.
        /// The application needs to have the keys/unwrapKey and keys/wrapKey permissions on key vault keys that need to be used in the encryption flow.
        /// </summary>
        /// <param name="clientId">Application Id.</param>
        /// <param name="clientSecret">A secret string that the application uses to prove its identity when requesting a token. Also can be referred to as application password.</param>
        public AzureKeyVaultKeyWrapProvider(string clientId, string clientSecret)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
        }

        /// <summary>
        /// Wraps (i.e. encrypts) the provided key.
        /// </summary>
        /// <param name="key">Key that needs to be wrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider.</param>
        /// <returns>Awaitable wrapped (i.e. encrypted) version of key passed in.</returns>
        public Task<KeyWrapResponse> WrapKeyAsync(byte[] key, KeyWrapMetadata metadata)
        {
            return null;
        }

        /// <summary>
        /// Unwraps (i.e. decrypts) the provided key.
        /// </summary>
        /// <param name="wrappedKey">Wrapped form of key that needs to be unwrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider.</param>
        /// <returns>Awaitable unwrapped (i.e. unencrypted) version of encrypted key passed in.</returns>
        public Task<KeyUnwrapResponse> UnwrapKeyAsync(byte[] wrappedKey, KeyWrapMetadata metadata)
        {
            return null;
        }
    }
}
