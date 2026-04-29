// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Serializes distributed transaction requests to JSON format.
    /// </summary>
    internal static class DistributedTransactionSerializer
    {
        internal const string Operations = "operations";
        internal const string DatabaseName = "databaseName";
        internal const string CollectionName = "collectionName";
        internal const string Id = "id";
        internal const string CollectionResourceId = "collectionResourceId";
        internal const string DatabaseResourceId = "databaseResourceId";
        internal const string PartitionKey = "partitionKey";
        internal const string Index = "index";
        internal const string ResourceBody = "resourceBody";
        internal const string SessionToken = "sessionToken";
        internal const string ETag = "ifMatch";
        internal const string OperationType = "operationType";
        internal const string ResourceType = "resourceType";

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
                jsonWriter.WriteStartArray(Operations);

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
            jsonWriter.WriteString(DatabaseName, operation.Database);

            // collectionName
            jsonWriter.WriteString(CollectionName, operation.Container);

            // id
            jsonWriter.WriteString(Id, operation.Id);

            // collectionResourceId
            if (operation.CollectionResourceId != null)
            {
                jsonWriter.WriteString(CollectionResourceId, operation.CollectionResourceId);
            }

            // databaseResourceId
            if (operation.DatabaseResourceId != null)
            {
                jsonWriter.WriteString(DatabaseResourceId, operation.DatabaseResourceId);
            }

            // partitionKey
            if (operation.PartitionKeyJson != null)
            {
                jsonWriter.WritePropertyName(PartitionKey);
                jsonWriter.WriteRawValue(operation.PartitionKeyJson, skipInputValidation: true);
            }

            // index (uint32)
            if (operation.OperationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operation.OperationIndex), "Operation index must be non-negative.");
            }
            jsonWriter.WriteNumber(Index, (uint)operation.OperationIndex);

            //resourceBody - written as nested JSON object
            if (!operation.ResourceBody.IsEmpty)
            {
                jsonWriter.WritePropertyName(ResourceBody);
                jsonWriter.WriteRawValue(operation.ResourceBody.Span, skipInputValidation: true);
            }

            // sessionToken
            if (operation.SessionToken != null)
            {
                jsonWriter.WriteString(SessionToken, operation.SessionToken);
            }

            // etag
            if (operation.ETag != null)
            {
                jsonWriter.WriteString(ETag, operation.ETag);
            }

            // operationType (string)
            jsonWriter.WriteString(OperationType, operation.OperationType.ToString());
            // resourceType (string)
            jsonWriter.WriteString(ResourceType, Documents.ResourceType.Document.ToString());

            jsonWriter.WriteEndObject();
        }
    }
}
