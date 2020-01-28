//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    using Microsoft.Azure.Documents;

    internal enum KeyVaultErrorCode
    {
        AadClientCredentialsGrantFailure = SubStatusCodes.AadClientCredentialsGrantFailure, // Indicated access to AAD failed to get a token
        AadServiceUnavailable = SubStatusCodes.AadServiceUnavailable, // Aad Service is Unavailable
        KeyVaultAuthenticationFailure = SubStatusCodes.KeyVaultAuthenticationFailure, // Indicate the KeyVault doesn't grant permission to the AAD, or the key is disabled.
        KeyVaultKeyNotFound = SubStatusCodes.KeyVaultKeyNotFound, // Indicate the Key Vault Key is not found
        KeyVaultServiceUnavailable = SubStatusCodes.KeyVaultServiceUnavailable, // Key Vault Service is Unavailable
        KeyVaultWrapUnwrapFailure = SubStatusCodes.KeyVaultWrapUnwrapFailure, // Indicate that Key Vault is unable to Wrap or Unwrap, one possible scenario is KeyVault failed to decoded the encrypted blob using the latest key because customer has rotated the key.
        InvalidKeyVaultKeyURI = SubStatusCodes.InvalidKeyVaultKeyURI, // Indicate the Key Vault Key URI is invalid.
        InvalidInputBytes = SubStatusCodes.InvalidInputBytes, // The input bytes are not in the format of base64.
        InternalServerError = SubStatusCodes.KeyVaultInternalServerError// Other unknown errors
    }
}

