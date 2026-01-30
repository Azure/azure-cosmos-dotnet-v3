// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Serializes distributed transaction requests to JSON format.
    /// </summary>
    internal static class DistributedTransactionSerializer
    {
        /// <summary>
        /// Serializes a distributed transaction request to a JSON stream.
        /// </summary>
        /// <param name="idempotencyToken">The idempotency token for the request.</param>
        /// <param name="operationType">The operation type.</param>
        /// <param name="resourceType">The resource type.</param>
        /// <param name="operations">The list of operations to include in the request.</param>
        /// <param name="headers">Optional headers to include in the request.</param>
        /// <returns>A MemoryStream containing the JSON-serialized request.</returns>
        public static MemoryStream SerializeRequest(
            Guid idempotencyToken,
            OperationType operationType,
            ResourceType resourceType,
            IReadOnlyList<DistributedTransactionOperation> operations,
            IDictionary<string, string> headers = null)
        {
            MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                jsonWriter.WriteStartObject();

                // idempotencyToken
                jsonWriter.WriteString("idempotencyToken", idempotencyToken.ToString());

                // rntbdOperationType (stringified)
                jsonWriter.WriteString("rntbdOperationType", ((int)operationType).ToString());

                // rntbdResourceType (stringified)
                jsonWriter.WriteString("rntbdResourceType", ((int)resourceType).ToString());

                // headers (optional)
                if (headers != null && headers.Count > 0)
                {
                    jsonWriter.WriteStartObject("headers");
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        jsonWriter.WriteString(header.Key, header.Value);
                    }
                    jsonWriter.WriteEndObject();
                }

                // operations
                jsonWriter.WriteStartArray("operations");

                foreach (DistributedTransactionOperation operation in operations)
                {
                    WriteOperation(jsonWriter, operation);
                }

                jsonWriter.WriteEndArray();

                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
            }

            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Writes a single operation to the JSON writer.
        /// Keys match the C++ DistributedTransactionOperation model.
        /// </summary>
        private static void WriteOperation(Utf8JsonWriter jsonWriter, DistributedTransactionOperation operation)
        {
            jsonWriter.WriteStartObject();

            // databaseName
            if (operation.Database != null)
            {
                jsonWriter.WriteString("databaseName", operation.Database);
            }

            // collectionId
            if (operation.Container != null)
            {
                jsonWriter.WriteString("collectionName", operation.Container);
            }
            // collectionResourceId
            if (operation.CollectionResourceId != null)
            {
                jsonWriter.WriteString("collectionResourceId", operation.CollectionResourceId);
            }

            // databaseResourceId
            if (operation.DatabaseResourceId != null)
            {
                jsonWriter.WriteString("databaseResourceId", operation.DatabaseResourceId);
            }

            // id 
            if (operation.Id != null)
            {
                jsonWriter.WriteString("resourceId", operation.Id);
            }

            // partitionKey
            if (operation.PartitionKeyJson != null)
            {
                jsonWriter.WriteString("partitionKey", operation.PartitionKeyJson);
            }

            // index (stringified as uint32)
            if (operation.OperationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operation.OperationIndex), "Operation index must be non-negative.");
            }
            jsonWriter.WriteString("index", operation.OperationIndex.ToString());

            // body (base64 encoded resourceBody)
            if (!operation.ResourceBody.IsEmpty)
            {
                jsonWriter.WriteBase64String("body", operation.ResourceBody.Span);
            }

            // sessionToken
            if (operation.SessionToken != null)
            {
                jsonWriter.WriteString("sessionToken", operation.SessionToken);
            }

            // etag
            if (operation.ETag != null)
            {
                jsonWriter.WriteString("etag", operation.ETag);
            }

            // operationType (RntbdOperationType as int)
            jsonWriter.WriteString("operationType", ((int)operation.OperationType).ToString());

            // resourceType (RntbdResourceType as int)
            jsonWriter.WriteString("resourceType", ((int)ResourceType.Document).ToString());

            // headers (empty for now, can be extended)
            jsonWriter.WriteStartObject("headers");
            jsonWriter.WriteEndObject();

            jsonWriter.WriteEndObject();
        }
    }
}
