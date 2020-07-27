//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
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
            cancellationToken: default(CancellationToken));

            TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                batchRequest,
                MockCosmosUtil.Serializer,
                true,
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
            cancellationToken: default(CancellationToken));

            PointOperationStatistics diagnostics = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: "http://localhost",
                requestSessionToken: null,
                responseSessionToken: null);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent,
            };

            responseMessage.DiagnosticsContext.AddDiagnosticsInternal(diagnostics);

            TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                responseMessage,
                batchRequest,
                MockCosmosUtil.Serializer,
                true,
                CancellationToken.None);

            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(arrayOperations.Length, batchresponse, MockCosmosUtil.Serializer);

            string pointDiagnosticString = diagnostics.ToString();
            pointDiagnosticString = pointDiagnosticString.Substring(1, pointDiagnosticString.Length - 2);
            string diagnosticContextString = response.DiagnosticsContext.ToString();
            Assert.IsTrue(diagnosticContextString.Contains(pointDiagnosticString));
        }
    }
}
