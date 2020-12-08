//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using global::Azure.Core;
    using global::Azure.Security.KeyVault.Keys;

    /// <summary>
    /// Factory Class for Accessing KeyClient methods.
    /// </summary>
    internal class KeyClientFactory
    {
        public virtual KeyClient GetKeyClient(KeyVaultKeyUriProperties keyVaultKeyUriProperties, TokenCredential tokenCredential)
        {
            return new KeyClient(keyVaultKeyUriProperties.KeyVaultUri, tokenCredential);
        }
    }
}
