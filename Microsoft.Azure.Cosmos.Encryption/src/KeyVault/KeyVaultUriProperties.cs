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
        public KeyVaultUriProperties(Uri parsingUri)
        {
            this.ParsingUri = parsingUri;
        }

        public Uri ParsingUri { get; private set; }

        public string KeyName { get; private set; }

        public string KeyValtName { get; private set; }

        public string KeyVersion { get; private set; }

        public Uri KeyVaultUri { get; private set; }

        public void TryParseUri()
        {
            if (!(this.ParsingUri.Segments.Length == 4 && this.ParsingUri.Segments[1] == KeyVaultConstants.KeysSegment))
            {
                throw new KeyVaultAccessException(
                            HttpStatusCode.NotFound,
                            KeyVaultErrorCode.KeyVaultServiceUnavailable,
                            "KeyVaultAccessClient:TryParseUri Failed to parse Uri to fetch relevant information");
            }

            this.KeyName = this.ParsingUri.Segments[2];
            this.KeyVersion = this.ParsingUri.Segments[3];
            this.KeyValtName = this.ParsingUri.Host;
            this.KeyVaultUri = new Uri($"https://{this.KeyValtName}/");
        }
    }
}