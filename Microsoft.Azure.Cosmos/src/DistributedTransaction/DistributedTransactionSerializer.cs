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
        /// Serializes a distributed transaction request body to a JSON stream.
        /// The body contains only the operations array. Other metadata like idempotencyToken,
        /// operationType, and resourceType are sent as HTTP headers per the spec.
        /// </summary>
        /// <param name="operations">The list of operations to include in the request.</param>
        /// <returns>A MemoryStream containing the JSON-serialized request body.</returns>
        public static MemoryStream SerializeRequest(IReadOnlyList<DistributedTransactionOperation> operations)
        {
            MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                jsonWriter.WriteStartObject();

                // operations array
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
            jsonWriter.WriteString("databaseName", operation.Database);

            // collectionName
            jsonWriter.WriteString("collectionName", operation.Container);

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
                jsonWriter.WriteString("id", operation.Id);
            }

            // partitionKey
            if (operation.PartitionKeyJson != null)
            {
                jsonWriter.WritePropertyName("partitionKey");
                jsonWriter.WriteRawValue(operation.PartitionKeyJson, skipInputValidation: true);
            }

            // index (uint32)
            if (operation.OperationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operation.OperationIndex), "Operation index must be non-negative.");
            }
            jsonWriter.WriteNumber("index", (uint)operation.OperationIndex);

            //resourceBody - written as nested JSON object
            if (!operation.ResourceBody.IsEmpty)
            {
                jsonWriter.WritePropertyName("resourceBody");
                jsonWriter.WriteRawValue(operation.ResourceBody.Span, skipInputValidation: true);
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

            // operationType (string)
            jsonWriter.WriteString("operationType", operation.OperationType.ToString());

            // resourceType (string)
            jsonWriter.WriteString("resourceType", ResourceType.Document.ToString());

            jsonWriter.WriteEndObject();
        }
    }
}
