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
            return new ItemBatchOperation(OperationType.Create, 0, id, new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true));
        }

        [TestMethod]
        public async Task FitsAllOperations()
        {
            List<ItemBatchOperation> operations = new List<ItemBatchOperation>()
            {
                CreateItemBatchOperation(),
                CreateItemBatchOperation()
            };

            (PartitionKeyRangeServerBatchRequest request , ArraySegment<ItemBatchOperation> pendingOperations) = await PartitionKeyRangeServerBatchRequest.CreateAsync("0", new ArraySegment<ItemBatchOperation>(operations.ToArray()), 200000, 2, false, new CosmosJsonDotNetSerializer(), default(CancellationToken));

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
            (PartitionKeyRangeServerBatchRequest request, ArraySegment<ItemBatchOperation> pendingOperations) = await PartitionKeyRangeServerBatchRequest.CreateAsync("0", new ArraySegment<ItemBatchOperation>(operations.ToArray()), 200000, 1, false, new CosmosJsonDotNetSerializer(), default(CancellationToken));

            Assert.AreEqual(1, request.Operations.Count);
            Assert.AreEqual(operations[0].Id, request.Operations[0].Id);
            Assert.AreEqual(2, pendingOperations.Count);
            Assert.AreEqual(operations[1].Id, pendingOperations[0].Id);
            Assert.AreEqual(operations[2].Id, pendingOperations[1].Id);
        }
    }
}
