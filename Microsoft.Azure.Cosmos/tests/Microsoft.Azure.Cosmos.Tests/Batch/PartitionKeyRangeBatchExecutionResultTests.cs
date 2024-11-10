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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyRangeBatchExecutionResultTests
    {
        [TestMethod]
        public async Task ConstainsSplitIsTrue()
        {
            Assert.IsTrue(await this.ConstainsSplitIsTrueInternal(HttpStatusCode.Gone, SubStatusCodes.CompletingSplit));
            Assert.IsTrue(await this.ConstainsSplitIsTrueInternal(HttpStatusCode.Gone, SubStatusCodes.CompletingPartitionMigration));
            Assert.IsTrue(await this.ConstainsSplitIsTrueInternal(HttpStatusCode.Gone, SubStatusCodes.PartitionKeyRangeGone));
        }

        [TestMethod]
        public async Task ConstainsSplitIsFalse()
        {
            Assert.IsFalse(await this.ConstainsSplitIsTrueInternal(HttpStatusCode.OK, SubStatusCodes.Unknown));
            Assert.IsFalse(await this.ConstainsSplitIsTrueInternal((HttpStatusCode)429, SubStatusCodes.Unknown));
        }

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

            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(
                arrayOperations.Length,
                batchresponse,
                MockCosmosUtil.Serializer);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public void ToResponseMessage_MapsProperties()
        {
            PointOperationStatisticsTraceDatum pointOperationStatistics = new PointOperationStatisticsTraceDatum(
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

            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK)
            {
                ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                ETag = "1234",
                SubStatusCode = SubStatusCodes.CompletingSplit,
                RetryAfter = TimeSpan.FromSeconds(10),
                RequestCharge = 4.3,
                SessionToken = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid().ToString(),
            };

            using (ITrace trace = Trace.GetRootTrace("testtrace"))
            {
                result.Trace = trace;
                trace.AddDatum("Point Operation Statistics", pointOperationStatistics);
            }

            ResponseMessage response = result.ToResponseMessage();

            Assert.AreEqual(result.ResourceStream, response.Content);
            Assert.AreEqual(result.SubStatusCode, response.Headers.SubStatusCode);
            Assert.AreEqual(result.RetryAfter, response.Headers.RetryAfter);
            Assert.AreEqual(result.StatusCode, response.StatusCode);
            Assert.AreEqual(result.RequestCharge, response.Headers.RequestCharge);
            Assert.AreEqual(result.SessionToken, response.Headers.Session);
            Assert.AreEqual(result.ActivityId, response.Headers.ActivityId);
            string diagnostics = response.Diagnostics.ToString();
            Assert.IsNotNull(diagnostics);
            Assert.IsTrue(diagnostics.Contains(pointOperationStatistics.ActivityId));
        }

        private async Task<bool> ConstainsSplitIsTrueInternal(HttpStatusCode statusCode, SubStatusCodes subStatusCode)
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

            ResponseMessage response = new ResponseMessage(statusCode) { Content = responseContent };
            response.Headers.SubStatusCode = subStatusCode;

            TransactionalBatchResponse batchresponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                response,
                batchRequest,
                MockCosmosUtil.Serializer,
                true,
                NoOpTrace.Singleton,
                CancellationToken.None);

            PartitionKeyRangeBatchExecutionResult result = new PartitionKeyRangeBatchExecutionResult("0", arrayOperations, batchresponse);

            return result.IsSplit();
        }
    }
}