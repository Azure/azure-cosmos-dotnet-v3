//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Test-only helper that reads a HybridRow bulk batch body, removes the trailing (appended
    /// "id") component from every operation's partition key, and re-serializes an equivalent batch
    /// body. This lets an emulator container that declares a single partition key path accept the
    /// id-appended bulk operations produced by the SDK's "append id to the last partition key path"
    /// retry, simulating a backend that has already been migrated to the two-component key.
    /// </summary>
    internal static class BulkBatchBodyRewriter
    {
        public static async Task<Stream> StripLastPartitionKeyComponentAsync(
            Stream payload,
            string partitionKeyRangeId,
            CosmosSerializerCore serializerCore,
            CancellationToken cancellationToken)
        {
            List<ParsedOperation> parsedOperations = await BulkBatchBodyRewriter.ReadOperationsAsync(payload);

            ItemBatchOperation[] rewrittenOperations = new ItemBatchOperation[parsedOperations.Count];
            for (int i = 0; i < parsedOperations.Count; i++)
            {
                ParsedOperation parsed = parsedOperations[i];
                rewrittenOperations[i] = new ItemBatchOperation(
                    operationType: parsed.OperationType,
                    operationIndex: i,
                    partitionKey: Cosmos.PartitionKey.Null,
                    id: parsed.Id,
                    requestOptions: null)
                {
                    PartitionKeyJson = BulkBatchBodyRewriter.StripLastComponent(parsed.PartitionKeyJson),
                    ResourceBody = parsed.ResourceBody,
                };
            }

            (PartitionKeyRangeServerBatchRequest request, _) = await PartitionKeyRangeServerBatchRequest.CreateAsync(
                partitionKeyRangeId,
                new ArraySegment<ItemBatchOperation>(rewrittenOperations),
                maxBodyLength: int.MaxValue,
                maxOperationCount: int.MaxValue,
                ensureContinuousOperationIndexes: false,
                serializerCore: serializerCore,
                isClientEncrypted: false,
                intendedCollectionRidValue: null,
                cancellationToken: cancellationToken);

            MemoryStream body = request.TransferBodyStream();
            body.Position = 0;
            return body;
        }

        private static string StripLastComponent(string partitionKeyJson)
        {
            if (string.IsNullOrEmpty(partitionKeyJson))
            {
                return partitionKeyJson;
            }

            JArray components = JArray.Parse(partitionKeyJson);
            if (components.Count > 1)
            {
                components.RemoveAt(components.Count - 1);
            }

            return components.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static async Task<List<ParsedOperation>> ReadOperationsAsync(Stream payload)
        {
            List<ParsedOperation> operations = new List<ParsedOperation>();

#pragma warning disable CS0618 // Type or member is obsolete
            await payload.ReadRecordIOAsync(
                record =>
                {
                    Result r = BulkBatchBodyRewriter.ReadOperation(record, out ParsedOperation operation);
                    if (r != Result.Success)
                    {
                        return r;
                    }

                    operations.Add(operation);
                    return r;
                },
                resizer: new MemorySpanResizer<byte>((int)payload.Length));
#pragma warning restore CS0618 // Type or member is obsolete

            return operations;
        }

        private static Result ReadOperation(Memory<byte> input, out ParsedOperation operation)
        {
            RowBuffer row = new RowBuffer(input.Length);
            if (!row.ReadFrom(input.Span, HybridRowVersion.V1, BatchSchemaProvider.BatchLayoutResolver))
            {
                operation = null;
                return Result.Failure;
            }

            RowReader reader = new RowReader(ref row);
            return BulkBatchBodyRewriter.ReadOperation(ref reader, out operation);
        }

        private static Result ReadOperation(ref RowReader reader, out ParsedOperation operation)
        {
            operation = null;

            OperationType operationType = OperationType.Invalid;
            string partitionKeyJson = null;
            string id = null;
            byte[] resourceBody = null;

            while (reader.Read())
            {
                Result r;
                switch (reader.Path)
                {
                    case "operationType":
                        r = reader.ReadInt32(out int operationTypeInt);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        operationType = (OperationType)operationTypeInt;
                        break;

                    case "partitionKey":
                        r = reader.ReadString(out partitionKeyJson);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        break;

                    case "id":
                        r = reader.ReadString(out id);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        break;

                    case "resourceBody":
                        r = reader.ReadBinary(out resourceBody);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        break;
                }
            }

            if (operationType == OperationType.Invalid)
            {
                return Result.Failure;
            }

            operation = new ParsedOperation
            {
                OperationType = operationType,
                PartitionKeyJson = partitionKeyJson,
                Id = id,
                ResourceBody = resourceBody ?? Array.Empty<byte>(),
            };

            return Result.Success;
        }

        private sealed class ParsedOperation
        {
            public OperationType OperationType { get; set; }

            public string PartitionKeyJson { get; set; }

            public string Id { get; set; }

            public byte[] ResourceBody { get; set; }
        }
    }
}
