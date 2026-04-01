// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
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
        [JsonConstructor]
        protected DistributedTransactionOperationResult()
        {
        }

        /// <summary>
        /// Gets the index of this operation within the distributed transaction.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("index")]
        public virtual int Index { get; internal set; }

        /// <summary>
        /// Gets the HTTP status code returned by the operation.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("statuscode")]
        public virtual HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the HTTP status code returned by the operation indicates success.
        /// </summary>
        [JsonIgnore]
        public virtual bool IsSuccessStatusCode => (int)this.StatusCode >= 200 && (int)this.StatusCode <= 299;

        /// <summary>
        /// Gets the entity tag (ETag) associated with the operation result.
        /// The ETag is used for concurrency control and represents the version of the resource.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("etag")]
        public virtual string ETag { get; internal set; }

        /// <summary>
        /// Gets the session token associated with the operation result.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("sessionToken")]
        public virtual string SessionToken { get; internal set; }

        /// <summary>
        /// Gets the resource stream associated with the operation result.
        /// The stream contains the raw response payload returned by the operation.
        /// </summary>
        [JsonIgnore]
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// Used for JSON deserialization of the base64-encoded resource body.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("resourcebody")]
        internal string ResourceBodyBase64
        {
            get => null; // Write-only for deserialization
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    byte[] resourceBody = Convert.FromBase64String(value);
                    this.ResourceStream = new MemoryStream(resourceBody, 0, resourceBody.Length, writable: false, publiclyVisible: true);
                }
            }
        }

        /// <summary>
        /// Request charge in request units for the operation.
        /// </summary>
        [JsonPropertyName("requestCharge")]
        internal virtual double RequestCharge { get; set; }

        [JsonPropertyName("substatuscode")]
        internal virtual SubStatusCodes SubStatusCode { get; set; }

        /// <summary>
        /// ActivityId related to the operation.
        /// </summary>
        [JsonIgnore]
        internal virtual string ActivityId { get; set; }

        [JsonIgnore]
        internal ITrace Trace { get; set; }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionOperationResult"/> from a JSON element.
        /// </summary>
        /// <param name="json">The JSON element containing the operation result.</param>
        /// <returns>The deserialized operation result.</returns>
        internal static DistributedTransactionOperationResult FromJson(JsonElement json)
        {
            return JsonSerializer.Deserialize<DistributedTransactionOperationResult>(json);
        }
    }
}
