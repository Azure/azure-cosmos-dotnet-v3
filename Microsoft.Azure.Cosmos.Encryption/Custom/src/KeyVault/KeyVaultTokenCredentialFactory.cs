//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;

    /// <summary>
    /// Factory Class for Acquiring the TokenCredentails depending on the Type of Method.
    /// </summary>
    public abstract class KeyVaultTokenCredentialFactory
    {
        /// <summary>
        /// Implements an interface to get TokenCredentials.
        /// </summary>
        /// <param name="keyVaultKeyUri"> Azure Key-Vault Key URI to acquire a TokenCredendials for</param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> TokenCredentails </returns>
        public abstract ValueTask<TokenCredential> GetTokenCredentialAsync(Uri keyVaultKeyUri, CancellationToken cancellationToken);
    }
}
