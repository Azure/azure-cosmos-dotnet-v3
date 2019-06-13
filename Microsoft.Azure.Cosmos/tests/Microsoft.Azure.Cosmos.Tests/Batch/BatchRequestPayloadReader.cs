//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal class BatchRequestPayloadReader
    {
        private List<ItemBatchOperation> operations = new List<ItemBatchOperation>();

        internal async Task<List<ItemBatchOperation>> ReadPayloadAsync(Stream payload)
        {
            await payload.ReadRecordIOAsync(
                record =>
                {

                    Result r = this.ReadOperation(record, out ItemBatchOperation operation);
                    if (r != Result.Success)
                    {
                        return r;
                    }

                    this.operations.Add(operation);
                    return r;
                },
                resizer: new MemorySpanResizer<byte>((int)payload.Length));

            return this.operations;
        }

        private Result ReadOperation(Memory<byte> input, out ItemBatchOperation operation)
        {
            RowBuffer row = new RowBuffer(input.Length);
            if (!row.ReadFrom(input.Span, HybridRowVersion.V1, BatchSchemaProvider.BatchLayoutResolver))
            {
                operation = null;
                return Result.Failure;
            }

            RowReader reader = new RowReader(ref row);
            return BatchRequestPayloadReader.ReadOperation(ref reader, this.operations.Count, out operation);
        }

        private static Result ReadOperation(ref RowReader reader, int operationIndex, out ItemBatchOperation operation)
        {
            operation = null;

            OperationType operationType = OperationType.Invalid;
            string partitionKeyJson = null;
            byte[] effectivePartitionKey = null;
            string id = null;
            byte[] binaryId = null;
            byte[] resourceBody = null;
            Cosmos.IndexingDirective? indexingDirective = null;
            string ifMatch = null;
            string ifNoneMatch = null;
            int? ttlInSeconds = null;

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

                    case "resourceType":
                        r = reader.ReadInt32(out int resourceType);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        Assert.AreEqual(ResourceType.Document, (ResourceType)resourceType);
                        break;

                    case "partitionKey":
                        r = reader.ReadString(out partitionKeyJson);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        break;

                    case "effectivePartitionKey":
                        r = reader.ReadBinary(out effectivePartitionKey);
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

                    case "binaryId":
                        r = reader.ReadBinary(out binaryId);
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

                    case "indexingDirective":
                        r = reader.ReadString(out string indexingDirectiveStr);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        if (!Enum.TryParse<Cosmos.IndexingDirective>(indexingDirectiveStr, out Cosmos.IndexingDirective indexingDirectiveEnum))
                        {
                            return Result.Failure;
                        }

                        indexingDirective = indexingDirectiveEnum;

                        break;

                    case "ifMatch":
                        r = reader.ReadString(out ifMatch);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        break;

                    case "ifNoneMatch":
                        r = reader.ReadString(out ifNoneMatch);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        break;

                    case "timeToLiveInSeconds":
                        r = reader.ReadInt32(out int ttl);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        ttlInSeconds = ttl;
                        break;
                }
            }

            // Ensure the mandatory fields were populated
            if (operationType == OperationType.Invalid)
            {
                return Result.Failure;
            }

            ItemRequestOptions requestOptions = null;
            if (indexingDirective.HasValue || ifMatch != null || ifNoneMatch != null || binaryId != null || effectivePartitionKey != null || ttlInSeconds.HasValue)
            {
                requestOptions = new ItemRequestOptions();
                if (indexingDirective.HasValue)
                {
                    requestOptions.IndexingDirective = indexingDirective;
                }

                if (ifMatch != null)
                {
                    requestOptions.IfMatchEtag = ifMatch;
                }
                else if (ifNoneMatch != null)
                {
                    requestOptions.IfNoneMatchEtag = ifNoneMatch;
                }

                if (binaryId != null || effectivePartitionKey != null || ttlInSeconds.HasValue)
                {
                    requestOptions.Properties = new Dictionary<string, object>();

                    if (binaryId != null)
                    {
                        requestOptions.Properties.Add(WFConstants.BackendHeaders.BinaryId, binaryId);
                    }

                    if (effectivePartitionKey != null)
                    {
                        requestOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKey, effectivePartitionKey);
                    }

                    if (ttlInSeconds.HasValue)
                    {
                        requestOptions.Properties.Add(WFConstants.BackendHeaders.TimeToLiveInSeconds, ttlInSeconds.ToString());
                    }
                }
            }

            Documents.PartitionKey parsedPartitionKey = null;
            if (partitionKeyJson != null)
            {
                parsedPartitionKey = Documents.PartitionKey.FromJsonString(partitionKeyJson);
            }

            operation = new ItemBatchOperation(
                operationType: operationType,
                operationIndex: operationIndex,
                id: id,
                requestOptions: requestOptions)
            {
                ParsedPartitionKey = parsedPartitionKey,
                ResourceBody = resourceBody
            };

            return Result.Success;
        }
    }
}
