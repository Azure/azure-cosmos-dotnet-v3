//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;

    internal sealed class EncryptionTransactionalBatchOperationResult : TransactionalBatchOperationResult
    {
        private readonly TransactionalBatchOperationResult response;
        private Stream decryptedResourceStream;

        public EncryptionTransactionalBatchOperationResult(TransactionalBatchOperationResult response, Stream decryptedResourceStream)
        {
            this.response = response;
            this.decryptedResourceStream = decryptedResourceStream;
        }

        public override Stream ResourceStream => this.decryptedResourceStream;

        public override HttpStatusCode StatusCode => this.response.StatusCode;

        public override bool IsSuccessStatusCode => this.response.IsSuccessStatusCode;

        public override string ETag => this.response.ETag;

        public override TimeSpan RetryAfter => this.response.RetryAfter;

        internal void DisposeDecryptedResourceStream()
        {
            Interlocked.Exchange(ref this.decryptedResourceStream, null)?.Dispose();
        }
    }
}