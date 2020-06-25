//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal sealed class KeyVaultUnwrapResult
    {
        public KeyVaultUnwrapResult(string unwrappedKeyBytesInBase64, Uri keyVaultKeyUri)
        {
            this.UnwrappedKeyBytesInBase64 = unwrappedKeyBytesInBase64;
            this.KeyVaultKeyUri = keyVaultKeyUri;
        }

        public string UnwrappedKeyBytesInBase64 { get; }

        public Uri KeyVaultKeyUri { get; }
    }
}
