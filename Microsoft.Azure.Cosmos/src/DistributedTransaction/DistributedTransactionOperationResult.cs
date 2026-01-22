// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents result of a specific operation in distributed transaction
    /// </summary>
#if INTERNAL
        public 
#else
    internal
#endif
    class DistributedTransactionOperationResult
    {
        internal DistributedTransactionOperationResult(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        internal DistributedTransactionOperationResult(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string eTag = null,
            Stream resourceStream = null,
            string sessionToken = null,
            TimeSpan retryAfter = default)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.ETag = eTag;
            this.ResourceStream = resourceStream;
            this.SessionToken = sessionToken;
            this.RetryAfter = retryAfter;
        }

        internal DistributedTransactionOperationResult(DistributedTransactionOperationResult other)
        {
            this.StatusCode = other.StatusCode;
            this.SubStatusCode = other.SubStatusCode;
            this.ETag = other.ETag;
            this.ResourceStream = other.ResourceStream;
            this.SessionToken = other.SessionToken;
            this.RetryAfter = other.RetryAfter;
            this.RequestCharge = other.RequestCharge;
            this.IdempotencyToken = other.IdempotencyToken;
            this.Trace = other.Trace;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionOperationResult"/> class.
        /// This protected constructor is intended for use by derived classes.
        /// </summary>
        protected DistributedTransactionOperationResult()
        {
        }

        /// <summary>
        /// Gets the HTTP status code returned by the operation.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the HTTP status code returned by the operation indicates success.
        /// </summary>
        public virtual bool IsSuccessStatusCode => ((int)this.StatusCode >= 200) && ((int)this.StatusCode <= 299);

        /// <summary>
        /// Gets the entity tag (ETag) associated with the operation result.
        /// The ETag is used for concurrency control and represents the version of the resource.
        /// </summary>
        public virtual string ETag { get; internal set; }

        /// <summary>
        /// Gets the amount of time to wait before retrying this operation if it was rate limited.
        /// </summary>
        public virtual TimeSpan RetryAfter { get; internal set; }

        internal virtual string SessionToken { get; set; }

        /// <summary>
        /// Gets the resource stream associated with the operation result.
        /// The stream contains the raw response payload returned by the operation.
        /// </summary>
        public virtual Stream ResourceStream { get; internal set; }

        internal virtual SubStatusCodes SubStatusCode { get; set; }

        internal virtual Guid IdempotencyToken { get; set; }

        /// <summary>
        /// Gets the request charge for this operation.
        /// </summary>
        internal virtual double RequestCharge { get; set; }

        internal ITrace Trace { get; set; }
    }

    /// <summary>
    /// Represents result of a specific operation in distributed transaction with a typed resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
#if INTERNAL
        public 
#else
    internal
#endif
    class DistributedTransactionOperationResult<T> : DistributedTransactionOperationResult
    {
        internal DistributedTransactionOperationResult(DistributedTransactionOperationResult result, T resource)
            : base(result)
        {
            this.Resource = resource;
        }

        protected DistributedTransactionOperationResult()
        {
        }
        public virtual T Resource { get; set; }
    }
}
