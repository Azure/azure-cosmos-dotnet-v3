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
#if PREVIEW
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
        internal DistributedTransactionOperationResult()
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
        /// Gets the resource stream associated with the operation result.
        /// The stream contains the raw response payload returned by the operation.
        /// </summary>
        /// <remarks>
        /// <b>Lifetime / ownership:</b> This stream is owned by the enclosing
        /// <see cref="DistributedTransactionResponse"/> and will be disposed when that response is disposed.
        /// Do not dispose it directly. To deserialize to a typed object, use
        /// <see cref="DistributedTransactionResponse.GetOperationResultAtIndex{T}"/>.
        /// </remarks>
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// Gets the number of request units consumed by this operation.
        /// </summary>
        public virtual double RequestCharge { get; internal set; }

        internal virtual SubStatusCodes SubStatusCode { get; set; }

        /// <summary>
        /// Gets the session token returned by the distributed transaction coordinator for
        /// this operation. Callers can pass this value back through
        /// <c>DistributedTransactionRequestOptions.SessionToken</c> on a subsequent DTx
        /// operation to enforce read-your-writes session consistency for that op.
        /// </summary>
        /// <remarks>
        /// In canonical <c>{pkRangeId}:{lsn}</c> format when the coordinator behaves
        /// correctly; <c>null</c> when the response omitted the token field.
        /// </remarks>
        public virtual string SessionToken { get; internal set; }

        internal virtual string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// ActivityId related to the operation.
        /// </summary>
        internal virtual string ActivityId { get; set; }

        internal ITrace Trace { get; set; }

        internal CosmosSerializerCore SerializerCore { get; set; }

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
            if (json.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"DTC operation result must be a JSON object, but was '{json.ValueKind}'.");
            }

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult();

            if (TryGetInt32Property(json, DistributedTransactionSerializer.Index, out int index))
            {
                result.Index = index;
            }

            if (TryGetInt32Property(json, DistributedTransactionSerializer.StatusCode, out int statusCode))
            {
                result.StatusCode = (HttpStatusCode)statusCode;
            }

            if (TryGetUInt32Property(json, DistributedTransactionSerializer.SubStatusCode, out uint subStatus))
            {
                result.SubStatusCode = (SubStatusCodes)subStatus;
            }

            if (TryGetProperty(json, DistributedTransactionSerializer.ResponseETag, out JsonElement etagEl) && etagEl.ValueKind == JsonValueKind.String)
            {
                result.ETag = etagEl.GetString();
            }

            if (TryGetProperty(json, DistributedTransactionSerializer.SessionToken, out JsonElement sessionTokenEl) && sessionTokenEl.ValueKind == JsonValueKind.String)
            {
                result.SessionToken = sessionTokenEl.GetString();
            }

            if (TryGetProperty(json, DistributedTransactionSerializer.PartitionKeyRangeId, out JsonElement pkRangeIdEl) && pkRangeIdEl.ValueKind == JsonValueKind.String)
            {
                result.PartitionKeyRangeId = pkRangeIdEl.GetString();
            }

            if (TryGetDoubleProperty(json, DistributedTransactionSerializer.RequestCharge, out double requestCharge))
            {
                result.RequestCharge = requestCharge;
            }

            if (TryGetProperty(json, DistributedTransactionSerializer.ResourceBody, out JsonElement resourceBody)
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

        internal static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool TryGetInt32Property(JsonElement element, string propertyName, out int value)
        {
            if (TryGetProperty(element, propertyName, out JsonElement propertyElement))
            {
                if (propertyElement.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonException($"'{propertyName}' must be a JSON number, but was '{propertyElement.ValueKind}'.");
                }

                if (!propertyElement.TryGetInt32(out value))
                {
                    throw new JsonException($"'{propertyName}' must be a 32-bit integer JSON number.");
                }

                return true;
            }

            value = default;
            return false;
        }

        private static bool TryGetUInt32Property(JsonElement element, string propertyName, out uint value)
        {
            if (TryGetProperty(element, propertyName, out JsonElement propertyElement))
            {
                if (propertyElement.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonException($"'{propertyName}' must be a JSON number, but was '{propertyElement.ValueKind}'.");
                }

                if (!propertyElement.TryGetUInt32(out value))
                {
                    throw new JsonException($"'{propertyName}' must be a 32-bit unsigned integer JSON number.");
                }

                return true;
            }

            value = default;
            return false;
        }

        private static bool TryGetDoubleProperty(JsonElement element, string propertyName, out double value)
        {
            if (TryGetProperty(element, propertyName, out JsonElement propertyElement))
            {
                if (propertyElement.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonException($"'{propertyName}' must be a JSON number, but was '{propertyElement.ValueKind}'.");
                }

                if (!propertyElement.TryGetDouble(out value))
                {
                    throw new JsonException($"'{propertyName}' must be a double-precision JSON number.");
                }

                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Represents a typed result for a specific operation that was part of a <see cref="DistributedTransaction"/> request.
    /// </summary>
    /// <typeparam name="T">The type to which the resource body was deserialized.</typeparam>
#pragma warning disable SA1402 // File may only contain a single type
#if PREVIEW
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
