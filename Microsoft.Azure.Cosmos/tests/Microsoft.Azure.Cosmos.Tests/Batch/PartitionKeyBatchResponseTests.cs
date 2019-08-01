//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyBatchResponseTests
    {
        [TestMethod]
        [Owner("maquaran")]
        public void StatusCodesAreSet()
        {
            const string errorMessage = "some error";
            PartitionKeyBatchResponse response = new PartitionKeyBatchResponse(HttpStatusCode.NotFound, SubStatusCodes.ClientTcpChannelFull, errorMessage, null);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual(SubStatusCodes.ClientTcpChannelFull, response.SubStatusCode);
            Assert.AreEqual(errorMessage, response.ErrorMessage);
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task ConstainsSplitIsTrue()
        {
            Assert.IsTrue(await ConstainsSplitIsTrueInternal(HttpStatusCode.Gone, SubStatusCodes.CompletingSplit));
            Assert.IsTrue(await ConstainsSplitIsTrueInternal(HttpStatusCode.Gone, SubStatusCodes.CompletingPartitionMigration));
            Assert.IsTrue(await ConstainsSplitIsTrueInternal(HttpStatusCode.Gone, SubStatusCodes.PartitionKeyRangeGone));
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task ConstainsSplitIsFalse()
        {
            Assert.IsFalse(await ConstainsSplitIsTrueInternal(HttpStatusCode.OK, SubStatusCodes.Unknown));
            Assert.IsFalse(await ConstainsSplitIsTrueInternal((HttpStatusCode)429, SubStatusCodes.Unknown));
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task StatusCodesAreSetThroughResponseAsync()
        {
            List<BatchOperationResult> results = new List<BatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.AddComputeGatewayRequestCharges, 0, "0");

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
                maxBodyLength: 100,
                maxOperationCount: 1,
                serializer: new CosmosJsonDotNetSerializer(),
            cancellationToken: default(CancellationToken));

            BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                batchRequest,
                new CosmosJsonDotNetSerializer());

            PartitionKeyBatchResponse response = new PartitionKeyBatchResponse(new List<BatchResponse> { batchresponse }, new CosmosJsonDotNetSerializer());
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<bool> ConstainsSplitIsTrueInternal(HttpStatusCode statusCode, SubStatusCodes subStatusCode)
        {
            List<BatchOperationResult> results = new List<BatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            ItemBatchOperation operation = new ItemBatchOperation(OperationType.AddComputeGatewayRequestCharges, 0, "0");

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
                maxBodyLength: 100,
                maxOperationCount: 1,
                serializer: new CosmosJsonDotNetSerializer(),
            cancellationToken: default(CancellationToken));

            ResponseMessage response = new ResponseMessage(statusCode) { Content = responseContent };
            response.Headers.SubStatusCode = subStatusCode;

            BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                response,
                batchRequest,
                new CosmosJsonDotNetSerializer());

            PartitionKeyBatchResponse pkResponse = new PartitionKeyBatchResponse(new List<BatchResponse> { batchresponse }, new CosmosJsonDotNetSerializer());

            return pkResponse.ContainsSplit();
        }
    }
}
