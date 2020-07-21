//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Security;
    using System.Security.Cryptography;

    internal sealed class StringHMACSHA256Hash : IComputeHash
    {
        private readonly String base64EncodedKey;
        private readonly byte[] keyBytes;
        private SecureString secureString;

        public StringHMACSHA256Hash(String base64EncodedKey)
        {
            this.base64EncodedKey = base64EncodedKey;
            this.keyBytes = Convert.FromBase64String(base64EncodedKey);
        }

        public byte[] ComputeHash(MemoryStream bytesToHash)
        {
            using (HMACSHA256 hmacSha256 = new HMACSHA256(this.keyBytes))
            {
                return hmacSha256.ComputeHash(bytesToHash);
            }
        }

        public SecureString Key
        {
            get
            {
                if (this.secureString != null) return this.secureString;
                this.secureString = SecureStringUtility.ConvertToSecureString(this.base64EncodedKey);
                return this.secureString;
            }
        }

        public void Dispose()
        {
            if (this.secureString != null)
            {
                this.secureString.Dispose();
                this.secureString = null;
            }
        }
    }
}
