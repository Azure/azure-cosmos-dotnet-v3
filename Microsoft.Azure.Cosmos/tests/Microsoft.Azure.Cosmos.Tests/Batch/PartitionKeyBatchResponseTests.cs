//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyBatchResponseTests
    {
        [TestMethod]
        public async Task StatusCodesAreSetThroughResponseAsync()
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Read, 0, Cosmos.PartitionKey.Null, "0");

            results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Id
                    });

            arrayOperations[0] = operation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                batchRequest,
                MockCosmosUtil.Serializer,
                true,
                NoOpTrace.Singleton,
                CancellationToken.None);

            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(arrayOperations.Length, batchresponse, MockCosmosUtil.Serializer);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task DiagnosticsAreSetThroughResponseAsync()
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Read, 0, Cosmos.PartitionKey.Null, "0");

            results.Add(
                    new TransactionalBatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Id
                    });

            arrayOperations[0] = operation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent,
            };

            PointOperationStatisticsTraceDatum diagnostics = new PointOperationStatisticsTraceDatum(
                    activityId: Guid.NewGuid().ToString(),
                    statusCode: HttpStatusCode.OK,
                    subStatusCode: SubStatusCodes.Unknown,
                    responseTimeUtc: DateTime.UtcNow,
                    requestCharge: 0,
                    errorMessage: string.Empty,
                    method: HttpMethod.Get,
                    requestUri: "http://localhost",
                    requestSessionToken: null,
                    responseSessionToken: null,
                    beLatencyInMs: "0.42");

            TransactionalBatchResponse batchresponse;
            using (responseMessage.Trace = Trace.GetRootTrace("test trace"))
            {
                responseMessage.Trace.AddDatum("Point Operation Statistics", diagnostics);

                batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    batchRequest,
                    MockCosmosUtil.Serializer,
                    true,
                    responseMessage.Trace,
                    CancellationToken.None);
            }

            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(arrayOperations.Length, batchresponse, MockCosmosUtil.Serializer);

            if (!(response.Diagnostics is CosmosTraceDiagnostics cosmosTraceDiagnostics))
            {
                Assert.Fail();
                throw new Exception();
            }

            Assert.AreEqual(diagnostics, cosmosTraceDiagnostics.Value.Data.Values.First());
        }
    }
}