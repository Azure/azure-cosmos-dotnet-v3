//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface represents the available functions that can be done using a KeyVault Access client.
    /// </summary>
    internal abstract class IKeyVaultAccessClient
    {
        /// <summary>
        /// Unwrap the encrypted Key.
        /// Only supports encrypted bytes in base64 format.
        /// </summary>
        /// <param name="bytesInBase64">encrypted bytes encoded to base64 string. </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and decrypted bytes in base64 string format, can be convert to bytes using Convert.FromBase64String().</returns>
        public abstract Task<KeyVaultUnwrapResult> UnwrapKeyAsync(
               string bytesInBase64,
               Uri keyVaultKeyUri,
               CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Wrap the Key. 
        /// Only supports bytes in base64 format.
        /// </summary>
        /// <param name="bytesInBase64">bytes encoded to base64 string. E.g. Convert.ToBase64String(bytes) .</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and encrypted bytes in base64 string format.</returns>
        public abstract Task<KeyVaultWrapResult> WrapKeyAsync(
               string bytesInBase64,
               Uri keyVaultKeyUri,
               CancellationToken cancellationToken = default(CancellationToken));
			   
        /// <summary>
        /// Validate the Purge Protection And Soft Delete Settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether The Customer has the correct setting or not. </returns>
        public abstract Task<bool> ValidatePurgeProtectionAndSoftDeleteSettingsAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}

