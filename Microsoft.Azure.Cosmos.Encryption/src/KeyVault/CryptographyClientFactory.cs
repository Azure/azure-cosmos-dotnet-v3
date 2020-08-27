//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using global::Azure.Core;
    using global::Azure.Security.KeyVault.Keys.Cryptography;

    /// <summary>
    /// Factory Class for Accessing CryptographyClient methods.
    /// </summary>
    internal class CryptographyClientFactory
    {
        public virtual CryptographyClient GetCryptographyClient(KeyVaultKeyUriProperties keyVaultKeyUriProperties, TokenCredential tokenCredential)
        {
            return new CryptographyClient(keyVaultKeyUriProperties.KeyUri, tokenCredential);
        }
    }
}
