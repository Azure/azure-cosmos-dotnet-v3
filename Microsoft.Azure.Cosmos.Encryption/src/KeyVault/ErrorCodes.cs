//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal enum KeyVaultErrorCode
    {
        AadClientCredentialsGrantFailure = 4000, // SubStatusCodes.AadClientCredentialsGrantFailure, // Indicated access to AAD failed to get a token
        AadServiceUnavailable = 4001, // SubStatusCodes.AadServiceUnavailable, // Aad Service is Unavailable
        KeyVaultAuthenticationFailure = 4002, // SubStatusCodes.KeyVaultAuthenticationFailure, // Indicate the KeyVault doesn't grant permission to the AAD, or the key is disabled.
        KeyVaultKeyNotFound = 4003, // SubStatusCodes.KeyVaultKeyNotFound, // Indicate the Key Vault Key is not found
        KeyVaultServiceUnavailable = 4004, // SubStatusCodes.KeyVaultServiceUnavailable, // Key Vault Service is Unavailable
        KeyVaultWrapUnwrapFailure = 4005, // SubStatusCodes.KeyVaultWrapUnwrapFailure, // Indicate that Key Vault is unable to Wrap or Unwrap, one possible scenario is KeyVault failed to decoded the encrypted blob using the latest key because customer has rotated the key.
        InvalidKeyVaultKeyURI = 4006, // SubStatusCodes.InvalidKeyVaultKeyURI, // Indicate the Key Vault Key URI is invalid.
        InvalidInputBytes = 4007, // SubStatusCodes.InvalidInputBytes, // The input bytes are not in the format of base64.
        KeyVaultInternalServerError = 4008, // SubStatusCodes.KeyVaultInternalServerError // Other unknown errors
        KeyVaultDNSNotResolved = 4009 // SubStatusCodes.KeyVaultDNSNotResolved, // Key Vault DNS name could not be resolved, mostly due to customer enter incorrect KeyVault name.
    }
}

