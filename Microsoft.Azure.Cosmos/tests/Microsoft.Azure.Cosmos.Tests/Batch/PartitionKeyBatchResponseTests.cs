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
        public void StatusCodesAreSet()
        {
            const string errorMessage = "some error";
            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(HttpStatusCode.NotFound, SubStatusCodes.ClientTcpChannelFull, errorMessage, null);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual(SubStatusCodes.ClientTcpChannelFull, response.SubStatusCode);
            Assert.AreEqual(errorMessage, response.ErrorMessage);
        }

        [TestMethod]
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

            PartitionKeyRangeBatchResponse response = new PartitionKeyRangeBatchResponse(arrayOperations.Length, batchresponse, new CosmosJsonDotNetSerializer());
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
