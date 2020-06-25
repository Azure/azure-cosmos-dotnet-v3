//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal sealed class KeyVaultWrapResult
    {
        public KeyVaultWrapResult(string wrappedKeyBytesInBase64, Uri keyVaultKeyUri)
        {
            this.WrappedKeyBytesInBase64 = wrappedKeyBytesInBase64;
            this.KeyVaultKeyUri = keyVaultKeyUri;

            string[] segments = keyVaultKeyUri.Segments;
            this.KeyVersion = segments.Length < 4 ? string.Empty : segments[3];
        }

        public string WrappedKeyBytesInBase64 { get; }

        public Uri KeyVaultKeyUri { get; }

        public string KeyVersion { get; }
    }
}

