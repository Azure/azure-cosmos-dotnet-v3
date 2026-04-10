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
        /// <remarks>
        /// Must be <c>public</c> for System.Text.Json reflection-based deserialization.
        /// System.Text.Json 6.x only scans <c>BindingFlags.Public</c> constructors when resolving
        /// <see cref="JsonConstructorAttribute"/>; non-public constructors are not found.
        /// Support for non-public constructors was added in System.Text.Json 7.0.
        /// </remarks>
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

        /// <summary>
        /// Gets the sub-status code value as an unsigned integer.
        /// </summary>
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

        private static readonly JsonSerializerOptions CaseInsensitiveOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Creates a <see cref="DistributedTransactionOperationResult"/> from a JSON element.
        /// </summary>
        /// <param name="json">The JSON element containing the operation result.</param>
        /// <returns>The deserialized operation result.</returns>
        internal static DistributedTransactionOperationResult FromJson(JsonElement json)
        {
            DistributedTransactionOperationResult result = JsonSerializer.Deserialize<DistributedTransactionOperationResult>(json, DistributedTransactionOperationResult.CaseInsensitiveOptions);

            if (json.TryGetProperty("resourceBody", out JsonElement resourceBody)
                && resourceBody.ValueKind != JsonValueKind.Undefined
                && resourceBody.ValueKind != JsonValueKind.Null)
            {
                // resourceBody is expected to be a JSON object (Cosmos DB document)
                if (resourceBody.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException($"The 'resourceBody' value must be a JSON object, but was '{resourceBody.ValueKind}'.");
                }

                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(resourceBody);
                result.ResourceStream = new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
            }

            return result;
        }
    }
}
