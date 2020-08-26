//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultKeyUriProperties"/> class.
    /// Helper Class to fetch frequently used Uri parsed information for KeyVault.
    /// </summary>
    internal sealed class KeyVaultKeyUriProperties
    {
        private KeyVaultKeyUriProperties(Uri keyUri)
        {
            this.KeyUri = keyUri;
        }

        public Uri KeyUri { get; }

        public string KeyName { get; private set; }

        public string KeyVersion { get; private set; }

        public Uri KeyVaultUri { get; private set; }

        public static bool TryParse(Uri keyUri, out KeyVaultKeyUriProperties keyVaultUriProperties)
        {
            keyVaultUriProperties = null;

            if (!((keyUri.Segments.Length == 4) && string.Equals(keyUri.Segments[1], KeyVaultConstants.KeysSegment, StringComparison.InvariantCultureIgnoreCase)))
            {
                return false;
            }

            keyVaultUriProperties = new KeyVaultKeyUriProperties(keyUri);
            keyVaultUriProperties.KeyName = keyVaultUriProperties.KeyUri.Segments[2].TrimEnd('/');
            keyVaultUriProperties.KeyVersion = keyVaultUriProperties.KeyUri.Segments[3];
            keyVaultUriProperties.KeyVaultUri = new Uri(keyVaultUriProperties.KeyUri.GetLeftPart(UriPartial.Scheme | UriPartial.Authority));

            return true;
        }
    }
}