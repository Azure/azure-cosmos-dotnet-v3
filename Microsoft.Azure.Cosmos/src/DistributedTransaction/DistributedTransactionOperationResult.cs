// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Core.Trace;
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
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// Request charge in request units for the operation.
        /// </summary>
        public virtual double RequestCharge { get; internal set; }

        internal virtual SubStatusCodes SubStatusCode { get; set; }

        internal virtual string SessionToken { get; set; }

        internal virtual string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionOperationResult"/> from a JSON element.
        /// </summary>
        /// <param name="json">The JSON element containing the operation result.</param>
        /// <returns>The deserialized operation result with a canonical session token.</returns>
        internal static DistributedTransactionOperationResult FromJson(JsonElement json)
        {
            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult();

            if (TryGetProperty(json, "index", out JsonElement indexEl) && indexEl.TryGetInt32(out int index))
            {
                result.Index = index;
            }

            if (TryGetProperty(json, "statusCode", out JsonElement statusCodeEl) && statusCodeEl.TryGetInt32(out int statusCode))
            {
                result.StatusCode = (HttpStatusCode)statusCode;
            }

            if (TryGetProperty(json, "subStatusCode", out JsonElement subStatusEl) && subStatusEl.TryGetUInt32(out uint subStatus))
            {
                result.SubStatusCode = (SubStatusCodes)subStatus;
            }

            if (TryGetProperty(json, "etag", out JsonElement etagEl) && etagEl.ValueKind == JsonValueKind.String)
            {
                result.ETag = etagEl.GetString();
            }

            if (TryGetProperty(json, "sessionToken", out JsonElement sessionTokenEl) && sessionTokenEl.ValueKind == JsonValueKind.String)
            {
                result.SessionToken = sessionTokenEl.GetString();
            }

            if (TryGetProperty(json, "partitionKeyRangeId", out JsonElement pkRangeIdEl) && pkRangeIdEl.ValueKind == JsonValueKind.String)
            {
                result.PartitionKeyRangeId = pkRangeIdEl.GetString();
            }

            if (TryGetProperty(json, "requestCharge", out JsonElement requestChargeEl) && requestChargeEl.TryGetDouble(out double requestCharge))
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

            if (!string.IsNullOrWhiteSpace(result.SessionToken))
            {
                int colonIndex = result.SessionToken.IndexOf(':');
                if (colonIndex > 0 && colonIndex < result.SessionToken.Length - 1)
                {
                    // Already in canonical {pkRangeId}:{lsn} form — leave as-is.
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

        internal static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
        {
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
    }
}
