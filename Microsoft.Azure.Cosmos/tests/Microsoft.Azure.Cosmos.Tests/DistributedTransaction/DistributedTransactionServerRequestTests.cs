// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

    [TestClass]
    public class DistributedTransactionServerRequestTests
    {
        [TestMethod]
        [Description("Verifies that CreateBodyStream returns a new independent MemoryStream on each call, enabling safe retry — disposing one stream must not affect siblings, and all streams must contain identical serialized bytes.")]
        public async Task CreateBodyStream_CalledMultipleTimes_ReturnsIndependentStreams()
        {
            DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            using (MemoryStream stream1 = request.CreateBodyStream())
            using (MemoryStream stream2 = request.CreateBodyStream())
            {
                Assert.AreNotSame(stream1, stream2, "Each call must return a new stream instance.");
                Assert.AreEqual(0, stream1.Position, "stream1 must be positioned at offset 0.");
                Assert.AreEqual(0, stream2.Position, "stream2 must be positioned at offset 0.");
                Assert.IsTrue(stream1.Length > 0, "The serialized body must be non-empty.");
                Assert.AreEqual(stream1.Length, stream2.Length, "Both streams must contain the same number of bytes.");
                CollectionAssert.AreEqual(
                    stream1.ToArray(),
                    stream2.ToArray(),
                    "Both streams must contain identical serialized bytes.");
            }

            // Obtain a third stream after the first two have been disposed.
            using (MemoryStream stream3 = request.CreateBodyStream())
            {
                Assert.IsTrue(stream3.CanRead, "A stream obtained after disposing siblings must still be readable.");
                Assert.AreEqual(0, stream3.Position, "stream3 must be positioned at offset 0.");
            }
        }

        private static IReadOnlyList<DistributedTransactionOperation> CreateTestOperations()
        {
            return new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    database: "testDb",
                    container: "testContainer",
                    partitionKey: new PartitionKey("pk0")),
                new DistributedTransactionOperation(
                    OperationType.Upsert,
                    operationIndex: 1,
                    database: "testDb",
                    container: "testContainer",
                    partitionKey: new PartitionKey("pk1")),
            };
        }
    }
}
