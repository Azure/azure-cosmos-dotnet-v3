//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Security;
    using System.Security.Cryptography;

    internal sealed class StringHMACSHA256Hash : IComputeHash
    {
        private readonly byte[] keyBytes;
        private readonly ConcurrentQueue<HMACSHA256> hmacPool;

        public StringHMACSHA256Hash(string base64EncodedKey)
        {
            if (string.IsNullOrEmpty(base64EncodedKey))
            {
                throw new ArgumentNullException(nameof(base64EncodedKey));
            }

            this.Key = SecureStringUtility.ConvertToSecureString(base64EncodedKey);
            this.keyBytes = Convert.FromBase64String(base64EncodedKey);
            this.hmacPool = new ConcurrentQueue<HMACSHA256>();
        }

        public byte[] ComputeHash(ArraySegment<byte> bytesToHash)
        {
            if (this.hmacPool.TryDequeue(out HMACSHA256 hmacSha256))
            {
                hmacSha256.Initialize();
            }
            else
            {
                hmacSha256 = new HMACSHA256(this.keyBytes);
            }

            try
            {
                return hmacSha256.ComputeHash(bytesToHash.Array, 0, (int)bytesToHash.Count);
            }
            finally
            {
                this.hmacPool.Enqueue(hmacSha256);
            }
        }

        public SecureString Key { get; private set; }

        public void Dispose()
        {
            while (this.hmacPool.TryDequeue(out HMACSHA256 hmacsha256))
            {
                hmacsha256.Dispose();
            }

            if (this.Key != null)
            {
                this.Key.Dispose();
                this.Key = null;
            }
        }
    }
}
