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
        /// </summary>
        [JsonConstructor]
        public DistributedTransactionOperationResult()
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
        [JsonPropertyName("statusCode")]
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
        /// Request charge in request units for the operation.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("requestCharge")]
        public virtual double RequestCharge { get; internal set; }

        [JsonIgnore]
        internal virtual SubStatusCodes SubStatusCode { get; set; }

        [JsonInclude]
        [JsonPropertyName("subStatusCode")]
        public virtual uint SubStatusCodeValue
        {
            get => (uint)this.SubStatusCode;
            internal set => this.SubStatusCode = (SubStatusCodes)value;
        }

        /// <summary>
        /// ActivityId related to the operation.
        /// </summary>
        [JsonIgnore]
        internal virtual string ActivityId { get; set; }

        [JsonIgnore]
        internal ITrace Trace { get; set; }

        /// <summary>
        /// Gets or sets the resource body as a JSON element.
        /// Setting this property populates <see cref="ResourceStream"/> with the serialized bytes.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("resourceBody")]
        public virtual JsonElement? ResourceBody
        {
            get => null;
            internal set
            {
                if (value.HasValue
                    && value.Value.ValueKind != JsonValueKind.Undefined
                    && value.Value.ValueKind != JsonValueKind.Null)
                {
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value.Value);
                    this.ResourceStream = new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionOperationResult"/> from a JSON element.
        /// </summary>
        /// <param name="json">The JSON element containing the operation result.</param>
        /// <returns>The deserialized operation result.</returns>
        internal static DistributedTransactionOperationResult FromJson(JsonElement json)
        {
            return JsonSerializer.Deserialize<DistributedTransactionOperationResult>(json.GetRawText());
        }
    }
}
