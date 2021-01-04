//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class EncryptionTransactionalBatchOperationResult<T> : TransactionalBatchOperationResult<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchOperationResult{T}"/> class.
        /// </summary>
        /// <param name="result">BatchOperationResult with stream resource.</param>
        /// <param name="resource">Deserialized resource.</param>
        internal EncryptionTransactionalBatchOperationResult(T resource)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Gets or sets the content of the resource.
        /// </summary>
        public override T Resource { get; set; }
    }
}