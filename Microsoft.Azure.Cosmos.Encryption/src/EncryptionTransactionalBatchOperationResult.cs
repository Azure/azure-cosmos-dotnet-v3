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
            this.encryptionResourceStream = encryptionResourceStream;
            this.response = response;
        }

        public override Stream ResourceStream
        {
            get => this.encryptionResourceStream;
        }

        public override HttpStatusCode StatusCode => this.response.StatusCode;

        public override bool IsSuccessStatusCode => this.response.IsSuccessStatusCode;

        public override string ETag => this.response.ETag;

        public override TimeSpan RetryAfter => this.response.RetryAfter;
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class EncryptionTransactionalBatchOperationResult<T> : TransactionalBatchOperationResult<T>
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchOperationResult{T}"/> class.
        /// </summary>
        /// <param name="result">BatchOperationResult with stream resource.</param>
        /// <param name="resource">Deserialized resource.</param>
        internal EncryptionTransactionalBatchOperationResult(TransactionalBatchOperationResult result, T resource)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Gets or sets the content of the resource.
        /// </summary>
        public override T Resource { get; set; }
    }
}