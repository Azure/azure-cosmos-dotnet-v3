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
            List<BatchOperationResult> results = new List<BatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Read, 0, "0");

            results.Add(
                    new BatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Id
                    });

            arrayOperations[0] = operation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializer: new CosmosJsonDotNetSerializer(),
            cancellationToken: default(CancellationToken));

            BatchResponse batchresponse = await BatchResponse.FromResponseMessageAsync(
                new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                batchRequest,
                new CosmosJsonDotNetSerializer());

            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(arrayOperations.Length, batchresponse, new CosmosJsonDotNetSerializer());
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public void ToResponseMessage_MapsProperties()
        {
            BatchOperationResult result = new BatchOperationResult(HttpStatusCode.OK)
            {
                ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                ETag = "1234",
                SubStatusCode = SubStatusCodes.CompletingSplit,
                RetryAfter = TimeSpan.FromSeconds(10),
                RequestCharge = 4.3,
                Diagnostics = new PointOperationStatistics(Guid.NewGuid().ToString(), HttpStatusCode.OK, SubStatusCodes.Unknown, 0, string.Empty, HttpMethod.Get, new Uri("http://localhost"), new CosmosClientSideRequestStatistics())
            };

            ResponseMessage response = result.ToResponseMessage();

            Assert.AreEqual(result.ResourceStream, response.Content);
            Assert.AreEqual(result.SubStatusCode, response.Headers.SubStatusCode);
            Assert.AreEqual(result.RetryAfter, response.Headers.RetryAfter);
            Assert.AreEqual(result.StatusCode, response.StatusCode);
            Assert.AreEqual(result.RequestCharge, response.Headers.RequestCharge);
            Assert.AreEqual(result.Diagnostics, response.Diagnostics);
        }

        private async Task<bool> ConstainsSplitIsTrueInternal(HttpStatusCode statusCode, SubStatusCodes subStatusCode)
        {
            List<BatchOperationResult> results = new List<BatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.Read, 0, "0");

            results.Add(
                    new BatchOperationResult(HttpStatusCode.OK)
                    {
                        ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                        ETag = operation.Id
                    });

            arrayOperations[0] = operation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializer: new CosmosJsonDotNetSerializer(),
            cancellationToken: default(CancellationToken));

            ResponseMessage response = new ResponseMessage(statusCode) { Content = responseContent };
            response.Headers.SubStatusCode = subStatusCode;

            BatchResponse batchresponse = await BatchResponse.FromResponseMessageAsync(
                response,
                batchRequest,
                new CosmosJsonDotNetSerializer());

            PartitionKeyRangeBatchExecutionResult result = new PartitionKeyRangeBatchExecutionResult("0", arrayOperations, batchresponse);

            return result.IsSplit();
        }
    }
}
