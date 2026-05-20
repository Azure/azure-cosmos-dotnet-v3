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
    using Microsoft.Azure.Cosmos.Core.Trace;
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
            this.PartitionKeyRangeId = other.PartitionKeyRangeId;
            this.RequestCharge = other.RequestCharge;
            this.ActivityId = other.ActivityId;
            this.Trace = other.Trace;
            this.SerializerCore = other.SerializerCore;
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
        /// Gets the raw partition key range ID emitted by the server.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("partitionKeyRangeId")]
        public virtual string PartitionKeyRangeId { get; internal set; }

        /// <summary>
        /// Gets the resource stream associated with the operation result.
        /// The stream contains the raw response payload returned by the operation.
        /// </summary>
        /// <remarks>
        /// <b>Lifetime / ownership:</b> This stream is owned by the enclosing
        /// <see cref="DistributedTransactionResponse"/> and will be disposed when that response is disposed.
        /// Do not dispose it directly. To deserialize to a typed object, use
        /// <see cref="DistributedTransactionResponse.GetOperationResultAtIndex{T}"/>.
        /// </remarks>
        [JsonIgnore]
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("requestCharge")]
        public virtual double RequestCharge { get; internal set; }

        /// <summary>
        /// Gets the sub-status code for the operation.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("subStatusCode")]
        public virtual SubStatusCodes SubStatusCode { get; internal set; }

        /// <summary>
        /// ActivityId related to the operation.
        /// </summary>
        [JsonIgnore]
        internal virtual string ActivityId { get; set; }

        [JsonIgnore]
        internal ITrace Trace { get; set; }

        [JsonIgnore]
        internal CosmosSerializerCore SerializerCore { get; set; }

        private static readonly JsonSerializerOptions CaseInsensitiveOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Returns a fresh <see cref="Stream"/> that exposes the same bytes as <paramref name="source"/>
        /// without affecting the source stream's position or lifetime.
        /// </summary>
        internal static Stream CreateSnapshot(Stream source)
        {
            // Fast path for standard MemoryStream.
            if (source is MemoryStream ms
                && ms.GetType() == typeof(MemoryStream)
                && ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                return new MemoryStream(
                    buffer: buffer.Array,
                    index: buffer.Offset,
                    count: buffer.Count,
                    writable: false,
                    publiclyVisible: true);
            }

            // Fallback for other stream types. For seekable streams the position is reset
            // before and after copying. Non-seekable streams are copied from their current
            // position, which is acceptable here because the only producer of ResourceStream
            // is a freshly-created MemoryStream at position 0 (see FromJson).
            if (source.CanSeek)
            {
                source.Position = 0;
            }

            MemoryStream copy = new MemoryStream();
            source.CopyTo(copy);
            copy.Position = 0;

            if (source.CanSeek)
            {
                source.Position = 0;
            }

            return copy;
        }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionOperationResult"/> from a JSON element.
        /// </summary>
        /// <param name="json">The JSON element containing the operation result.</param>
        /// <returns>The deserialized operation result with a canonical session token.</returns>
        internal static DistributedTransactionOperationResult FromJson(JsonElement json)
        {
            DistributedTransactionOperationResult result = JsonSerializer.Deserialize<DistributedTransactionOperationResult>(json, DistributedTransactionOperationResult.CaseInsensitiveOptions)
                ?? throw new JsonException($"Failed to deserialize DTC operation result: Deserialize returned null. JSON element kind: '{json.ValueKind}'.");

            if (json.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out JsonElement resourceBody)
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

            if (!string.IsNullOrWhiteSpace(result.SessionToken))
            {
                int colonIndex = result.SessionToken.IndexOf(':');
                if (colonIndex > 0 && colonIndex < result.SessionToken.Length - 1)
                {
                    // Already in canonical SDK session-token format — leave as-is.
                }
                else if (!string.IsNullOrWhiteSpace(result.PartitionKeyRangeId))
                {
                    result.SessionToken = result.PartitionKeyRangeId + ":" + result.SessionToken;
                }
                else
                {
                    DefaultTrace.TraceWarning(
                        "DTC operation index {0} returned session token without a valid partitionKeyRangeId (value: '{1}'); session token will not be merged into the session container.",
                        result.Index,
                        result.PartitionKeyRangeId ?? "<absent>");
                    result.SessionToken = null;
                }
            }
            else if (result.SessionToken != null)
            {
                // Normalize whitespace-only to null so downstream guards don't need to recheck.
                result.SessionToken = null;
            }

            return result;
        }
    }

    /// <summary>
    /// Represents a typed result for a specific operation that was part of a <see cref="DistributedTransaction"/> request.
    /// </summary>
    /// <typeparam name="T">The type to which the resource body was deserialized.</typeparam>
#pragma warning disable SA1402 // File may only contain a single type
#if INTERNAL
    public
#else
    internal
#endif
    class DistributedTransactionOperationResult<T> : DistributedTransactionOperationResult
#pragma warning restore SA1402 // File may only contain a single type
    {
        internal DistributedTransactionOperationResult(DistributedTransactionOperationResult result, T resource)
            : base(result)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionOperationResult{T}"/> class.
        /// </summary>
        protected DistributedTransactionOperationResult()
        {
        }

        /// <summary>
        /// Gets the resource deserialized from the operation response body.
        /// Returns <c>default</c> when the server did not return a resource body for the operation
        /// (for example, a delete operation or a failed operation).
        /// </summary>
        public virtual T Resource { get; set; }
    }
}
