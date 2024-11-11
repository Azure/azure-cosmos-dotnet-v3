//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyRangeServerBatchRequestTests
    {
        private static ItemBatchOperation CreateItemBatchOperation(string id = "")
        {
            return new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null, id, new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));
        }

        [TestMethod]
        public async Task FitsAllOperations()
        {
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>()
            {
                CreateItemBatchOperation(),
                CreateItemBatchOperation()
            };

            (PartitionKeyRangeServerBatchRequest request, ArraySegment<ItemBatchOperation> pendingOperations) = await PartitionKeyRangeServerBatchRequest.CreateAsync(
                "0",
                new ArraySegment<ItemBatchOperation>(operations.ToArray()),
                200000,
                2,
                false,
                MockCosmosUtil.Serializer,
                isClientEncrypted: false,
                intendedCollectionRidValue: null,
                default);

            Assert.AreEqual(operations.Count, request.Operations.Count);
            CollectionAssert.AreEqual(operations, request.Operations.ToArray());
            Assert.AreEqual(0, pendingOperations.Count);
        }

        /// <summary>
        /// Verifies that the pending operations contain items that did not fit on the request
        /// </summary>
        [TestMethod]
        public async Task OverflowsBasedOnCount()
        {
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>()
            {
                CreateItemBatchOperation("1"),
                CreateItemBatchOperation("2"),
                CreateItemBatchOperation("3")
            };

            // Setting max count to 1
            (PartitionKeyRangeServerBatchRequest request, ArraySegment<ItemBatchOperation> pendingOperations) = await PartitionKeyRangeServerBatchRequest.CreateAsync(
                "0",
                new ArraySegment<ItemBatchOperation>(operations.ToArray()),
                200000,
                1,
                false,
                MockCosmosUtil.Serializer,
                isClientEncrypted: false,
                intendedCollectionRidValue: null,
                default);

            Assert.AreEqual(1, request.Operations.Count);
            Assert.AreEqual(operations[0].Id, request.Operations[0].Id);
            Assert.AreEqual(2, pendingOperations.Count);
            Assert.AreEqual(operations[1].Id, pendingOperations[0].Id);
            Assert.AreEqual(operations[2].Id, pendingOperations[1].Id);
        }

        /// <summary>
        /// Verifies that the pending operations algorithm takes into account Offset
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task OverflowsBasedOnCount_WithOffset()
        {
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>()
            {
                CreateItemBatchOperation("1"),
                CreateItemBatchOperation("2"),
                CreateItemBatchOperation("3")
            };

            // Setting max count to 1
            (PartitionKeyRangeServerBatchRequest request, ArraySegment<ItemBatchOperation> pendingOperations) = await PartitionKeyRangeServerBatchRequest.CreateAsync(
                "0",
                new ArraySegment<ItemBatchOperation>(operations.ToArray(), 1, 2),
                200000,
                1,
                false,
                MockCosmosUtil.Serializer,
                isClientEncrypted: false,
                intendedCollectionRidValue: null,
                default);

            Assert.AreEqual(1, request.Operations.Count);
            // The first element is not taken into account due to an Offset of 1
            Assert.AreEqual(operations[1].Id, request.Operations[0].Id);
            Assert.AreEqual(1, pendingOperations.Count);
            Assert.AreEqual(operations[2].Id, pendingOperations[0].Id);
        }

        [TestMethod]
        public async Task PartitionKeyRangeServerBatchRequestSizeTests()
        {
            const int docSizeInBytes = 250;
            const int operationCount = 10;

            foreach (int expectedOperationCount in new int[] { 1, 2, 5, 10 })
            {
                await PartitionKeyRangeServerBatchRequestTests.VerifyServerRequestCreationsBySizeAsync(expectedOperationCount, operationCount, docSizeInBytes);
                await PartitionKeyRangeServerBatchRequestTests.VerifyServerRequestCreationsByCountAsync(expectedOperationCount, operationCount, docSizeInBytes);
            }
        }

        private static async Task VerifyServerRequestCreationsBySizeAsync(
            int expectedOperationCount,
            int operationCount,
            int docSizeInBytes)
        {
            const int perRequestOverheadEstimateInBytes = 30;
            const int perDocOverheadEstimateInBytes = 50;
            int maxServerRequestBodyLength = ((docSizeInBytes + perDocOverheadEstimateInBytes) * expectedOperationCount) + perRequestOverheadEstimateInBytes;
            int maxServerRequestOperationCount = int.MaxValue;

            (PartitionKeyRangeServerBatchRequest request, ArraySegment<ItemBatchOperation> overflow) = await PartitionKeyRangeServerBatchRequestTests.GetBatchWithCreateOperationsAsync(operationCount, maxServerRequestBodyLength, maxServerRequestOperationCount, docSizeInBytes);

            Assert.AreEqual(expectedOperationCount, request.Operations.Count);
            Assert.AreEqual(overflow.Count, operationCount - request.Operations.Count);
        }

        private static async Task VerifyServerRequestCreationsByCountAsync(
            int expectedOperationCount,
            int operationCount,
            int docSizeInBytes)
        {
            int maxServerRequestBodyLength = int.MaxValue;
            int maxServerRequestOperationCount = expectedOperationCount;

            (PartitionKeyRangeServerBatchRequest request, ArraySegment<ItemBatchOperation> overflow) = await PartitionKeyRangeServerBatchRequestTests.GetBatchWithCreateOperationsAsync(operationCount, maxServerRequestBodyLength, maxServerRequestOperationCount, docSizeInBytes);

            Assert.AreEqual(expectedOperationCount, request.Operations.Count);
            Assert.AreEqual(overflow.Count, operationCount - request.Operations.Count);
        }

        private static async Task<Tuple<PartitionKeyRangeServerBatchRequest, ArraySegment<ItemBatchOperation>>> GetBatchWithCreateOperationsAsync(
            int operationCount,
            int maxServerRequestBodyLength,
            int maxServerRequestOperationCount,
            int docSizeInBytes = 20)
        {
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>();

            byte[] body = new byte[docSizeInBytes];
            Random random = new Random();
            random.NextBytes(body);
            for (int i = 0; i < operationCount; i++)
            {
                operations.Add(new ItemBatchOperation(OperationType.Create, 0, Cosmos.PartitionKey.Null, string.Empty, new MemoryStream(body)));
            }

            return await PartitionKeyRangeServerBatchRequest.CreateAsync("0",
                new ArraySegment<ItemBatchOperation>(operations.ToArray()),
                maxServerRequestBodyLength,
                maxServerRequestOperationCount,
                false,
                MockCosmosUtil.Serializer,
                isClientEncrypted: false,
                intendedCollectionRidValue: null,
                default);
        }
    }
}