//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProvidedTokenCredentialFactory"/> class.
    /// Class implements Client Certificate Based TokenCredential.
    /// Inits the TokenCredentials with user provided TokenCredential required for accessing Key Vault services.
    /// </summary>
    internal class UserProvidedTokenCredentialFactory : KeyVaultTokenCredentialFactory
    {
        private readonly TokenCredential tokenCredential;

        /// <summary>
        /// Takes a TokenCredentials which can be used to access keyVault services.
        /// </summary>
        /// <param name="tokenCredential"> TokenCredentials </param>
        public UserProvidedTokenCredentialFactory(TokenCredential tokenCredential)
        {
            if (tokenCredential != null)
            {
                this.tokenCredential = tokenCredential;
            }
            else
            {
                throw new ArgumentNullException("UserProvidedTokenCredentialFactory: Invalid null TokenCredentials Passed");
            }
        }

        /// <summary>
        /// Get the TokenCredentials for the Given KeyVaultKey URI
        /// </summary>
        /// <param name="keyVaultKeyUri"> Key-Vault Key  Uri </param>
        /// <param name="cancellationToken"> Cancellation token </param>
        /// <returns> User passed TokenCredential </returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async ValueTask<TokenCredential> GetTokenCredentialAsync(Uri keyVaultKeyUri, CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return this.tokenCredential;
        }
    }
}
