//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultUriProperties"/> class.
    /// Helper Class to fetch frequently used Uri parsed information for KeyVault.
    /// </summary>
    internal sealed class KeyVaultUriProperties
    {
        public KeyVaultUriProperties(Uri keyUri)
        {
            this.KeyUri = keyUri;
        }

        public Uri KeyUri { get; set; }

        public string KeyName { get; private set; }

        public string KeyVersion { get; private set; }

        public Uri KeyVaultUri { get; private set; }

        public static bool TryParseUri(Uri keyUri,out KeyVaultUriProperties keyVaultUriProperties)
        {
            keyVaultUriProperties = new KeyVaultUriProperties(keyUri);

            if (!((keyVaultUriProperties.KeyUri.Segments.Length == 3 || keyVaultUriProperties.KeyUri.Segments.Length == 4) &&
                string.Equals(keyVaultUriProperties.KeyUri.Segments[1], KeyVaultConstants.KeysSegment, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.InvalidKeyVaultKeyURI,
                            "KeyVaultAccessClient:TryParseUri Failed to parse Uri to fetch relevant information",
                            new ArgumentException(string.Format("KeyVault Key Uri:{0},Length:{1},KeySegment:{2} is invalid.", keyVaultUriProperties.KeyUri, keyVaultUriProperties.KeyUri.Segments.Length, keyVaultUriProperties.KeyUri.Segments[1])));
            }

            keyVaultUriProperties.KeyName = keyVaultUriProperties.KeyUri.Segments[2];
            keyVaultUriProperties.KeyVersion = keyVaultUriProperties.KeyUri.Segments[3];
            keyVaultUriProperties.KeyVaultUri = new Uri(keyVaultUriProperties.KeyUri.GetLeftPart(UriPartial.Scheme | UriPartial.Authority));

            return true;
        }
    }
}