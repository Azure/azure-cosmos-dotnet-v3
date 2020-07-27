//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class KeyVaultErrorCode
    {

        public const string AadServiceUnavailable = "AadServiceUnavailable"; // Aad Service is Unavailable

        public const string KeyVaultAuthenticationFailure = "KeyVaultAuthenticationFailure"; // Indicate the KeyVault doesn't grant permission to the AAD, or the key is disabled.

        public const string KeyVaultKeyNotFound = "KeyVaultKeyNotFound"; // Indicate the Key Vault Key is not found

        public const string KeyVaultServiceUnavailable = "KeyVaultServiceUnavailable"; // Key Vault Service is Unavailable

        public const string KeyVaultWrapUnwrapFailure = "KeyVaultWrapUnwrapFailure"; // Indicate that Key Vault is unable to Wrap or Unwrap, one possible scenario is KeyVault failed to decoded the encrypted blob using the latest key because customer has rotated the key.

        public const string InvalidKeyVaultKeyURI = "InvalidKeyVaultKeyURI"; // Indicate the Key Vault Key URI is invalid.

        public const string InvalidInputBytes = "InvalidInputBytes";  // The input bytes are not in the format of base64.

        public const string KeyVaultInternalServerError = "KeyVaultInternalServerError";  // Other unknown errors

        public const string KeyVaultDNSNotResolved = "KeyVaultDNSNotResolved";  // Key Vault DNS name could not be resolved, mostly due to customer enter incorrect KeyVault name.
    }
}