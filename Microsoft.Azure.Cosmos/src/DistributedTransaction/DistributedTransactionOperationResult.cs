// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents result of a specific operation in distributed transaction.
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

        internal DistributedTransactionOperationResult(DistributedTransactionOperationResult other)
        {
            this.Index = other.Index;
            this.StatusCode = other.StatusCode;
            this.SubStatusCode = other.SubStatusCode;
            this.ETag = other.ETag;
            this.ResourceStream = other.ResourceStream;
            this.SessionToken = other.SessionToken;
            this.RequestCharge = other.RequestCharge;
            this.ActivityId = other.ActivityId;
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
        /// Gets the index of this operation within the distributed transaction.
        /// </summary>
        public virtual int Index { get; internal set; }

        /// <summary>
        /// Gets the HTTP status code returned by the operation.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the HTTP status code returned by the operation indicates success.
        /// </summary>
        public virtual bool IsSuccessStatusCode => (int)this.StatusCode >= 200 && (int)this.StatusCode <= 299;

        /// <summary>
        /// Gets the entity tag (ETag) associated with the operation result.
        /// The ETag is used for concurrency control and represents the version of the resource.
        /// </summary>
        public virtual string ETag { get; internal set; }

        /// <summary>
        /// Gets the session token associated with the operation result.
        /// </summary>
        public virtual string SessionToken { get; internal set; }

        /// <summary>
        /// Gets the resource stream associated with the operation result.
        /// The stream contains the raw response payload returned by the operation.
        /// </summary>
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// Request charge in request units for the operation.
        /// </summary>
        internal virtual double RequestCharge { get; set; }

        internal virtual SubStatusCodes SubStatusCode { get; set; }

        /// <summary>
        /// ActivityId related to the operation.
        /// </summary>
        internal virtual string ActivityId { get; set; }

        internal ITrace Trace { get; set; }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionOperationResult"/> from a JSON element.
        /// </summary>
        /// <param name="json">The JSON element containing the operation result.</param>
        /// <returns>The deserialized operation result.</returns>
        internal static DistributedTransactionOperationResult FromJson(JsonElement json)
        {
            DistributedTransactionOperationResult operationResult = new DistributedTransactionOperationResult();

            if (json.TryGetProperty("index", out JsonElement indexElement))
            {
                operationResult.Index = indexElement.GetInt32();
            }

            if (json.TryGetProperty("statusCode", out JsonElement statusCodeElement))
            {
                operationResult.StatusCode = (HttpStatusCode)statusCodeElement.GetInt32();
            }

            if (json.TryGetProperty("substatuscode", out JsonElement subStatusCodeElement))
            {
                operationResult.SubStatusCode = (SubStatusCodes)subStatusCodeElement.GetInt32();
            }

            if (json.TryGetProperty("etag", out JsonElement eTagElement) && eTagElement.ValueKind != JsonValueKind.Null)
            {
                operationResult.ETag = eTagElement.GetString();
            }

            if (json.TryGetProperty("resourcebody", out JsonElement resourceBodyElement) && resourceBodyElement.ValueKind != JsonValueKind.Null)
            {
                string resourceBodyBase64 = resourceBodyElement.GetString();
                if (!string.IsNullOrEmpty(resourceBodyBase64))
                {
                    byte[] resourceBody = Convert.FromBase64String(resourceBodyBase64);
                    operationResult.ResourceStream = new MemoryStream(resourceBody, 0, resourceBody.Length, writable: false, publiclyVisible: true);
                }
            }

            if (json.TryGetProperty("sessionToken", out JsonElement sessionTokenElement) && sessionTokenElement.ValueKind != JsonValueKind.Null)
            {
                operationResult.SessionToken = sessionTokenElement.GetString();
            }

            if (json.TryGetProperty("requestCharge", out JsonElement requestChargeElement))
            {
                operationResult.RequestCharge = Math.Round(requestChargeElement.GetDouble(), 2);
            }

            return operationResult;
        }
    }
}
