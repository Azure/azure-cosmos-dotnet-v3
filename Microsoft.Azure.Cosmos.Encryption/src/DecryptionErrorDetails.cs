//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    public class DecryptionErrorDetails
    {
        /// <summary>
        /// The encrypted document returned as is (without decryption) in case of failure
        /// </summary>
        public ReadOnlyMemory<byte> EncryptedStream { get; }
        
        /// <summary>
        /// Represents the exception encountered.
        /// </summary>
        public Exception Exception { get; }

        public DecryptionErrorDetails(
            ReadOnlyMemory<byte> encryptedStream,
            Exception exception)
        {
            this.EncryptedStream = encryptedStream;
            this.Exception = exception;
        }
    }
}
