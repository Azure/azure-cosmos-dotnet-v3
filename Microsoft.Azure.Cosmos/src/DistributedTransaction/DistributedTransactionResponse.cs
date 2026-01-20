// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the response for a distributed transaction operation.
    /// </summary>
#if INTERNAL
        public 
#else
    internal
#endif
    class DistributedTransactionResponse : IReadOnlyList<DistributedTransactionOperationResult>
    {
        private readonly List<DistributedTransactionOperationResult> results = null;

        private DistributedTransactionResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            Headers headers,
            IReadOnlyList<DistributedTransactionOperation> operations)
        {
            this.Headers = headers;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.ErrorMessage = errorMessage;
            this.Operations = operations;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionResponse"/> class.
        /// </summary>
        protected DistributedTransactionResponse()
        {
        }

        /// <summary>
        /// Gets the <see cref="DistributedTransactionOperationResult"/> at the specified index in the response.
        /// </summary>
        /// <param name="index">The zero-based index of the operation result to get.</param>
        /// <returns>The <see cref="DistributedTransactionOperationResult"/> at the specified index.</returns>
        public virtual DistributedTransactionOperationResult this[int index]
        {
            get
            {
                if (this.results == null || index < 0 || index >= this.results.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
                }

                return this.results[index];
            }
        }

        /// <summary>
        /// Gets the headers associated with the distributed transaction response.
        /// </summary>
        public virtual Headers Headers { get; internal set; }

        /// <summary>
        /// Gets the HTTP status code for the distributed transaction response.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; internal set; }

        internal virtual SubStatusCodes SubStatusCode { get; }

        internal IReadOnlyList<DistributedTransactionOperation> Operations { get; set; }

        /// <summary>
        /// Gets the error message associated with the distributed transaction response, if any.
        /// </summary>
        public virtual string ErrorMessage { get; internal set; }

        /// <summary>
        /// Gets the number of operation results in the distributed transaction response.
        /// </summary>
        public virtual int Count => this.results?.Count ?? 0;

        /// <summary>
        /// Returns an enumerator that iterates through the collection of distributed transaction operation results.
        /// </summary>
        /// <returns>An enumerator for the collection of <see cref="DistributedTransactionOperationResult"/> objects.</returns>
        public virtual IEnumerator<DistributedTransactionOperationResult> GetEnumerator()
        {
            return this.results.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private void CreateAndPopulateResults(IReadOnlyList<DistributedTransactionOperation> operations, ITrace trace)
        {
            throw new NotImplementedException();
        }
    }
}
