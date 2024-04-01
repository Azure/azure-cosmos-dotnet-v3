//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Net;

    internal sealed class EncryptionTransactionalBatchOperationResult : TransactionalBatchOperationResult
    {
        private readonly Stream encryptionResourceStream;
        private readonly TransactionalBatchOperationResult response;

        public EncryptionTransactionalBatchOperationResult(TransactionalBatchOperationResult response, Stream encryptionResourceStream)
        {
            this.response = response;
            this.encryptionResourceStream = encryptionResourceStream;
        }

        public override Stream ResourceStream => this.encryptionResourceStream;

        public override HttpStatusCode StatusCode => this.response.StatusCode;

        public override bool IsSuccessStatusCode => this.response.IsSuccessStatusCode;

        public override string ETag => this.response.ETag;

        public override TimeSpan RetryAfter => this.response.RetryAfter;
    }
}