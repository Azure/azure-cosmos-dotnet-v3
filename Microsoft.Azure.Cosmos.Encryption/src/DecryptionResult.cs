//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Represents the details of failure encountered while decrypting response content.
    /// </summary>
    public sealed class DecryptionResult
    {
        /// <summary>
        /// Gets the encrypted document returned as is (without decryption) in case of failure
        /// </summary>
        public ReadOnlyMemory<byte> EncryptedStream { get; }

        /// <summary>
        /// Gets the exception encountered.
        /// </summary>
        public Exception Exception { get; }

        private DecryptionResult(
            ReadOnlyMemory<byte> encryptedStream,
            Exception exception)
        {
            this.EncryptedStream = encryptedStream;
            this.Exception = exception;
        }

        internal static DecryptionResult CreateFailure(
            ReadOnlyMemory<byte> encryptedStream,
            Exception exception)
        {
            return new DecryptionResult(
                encryptedStream,
                exception);
        }
    }
}
