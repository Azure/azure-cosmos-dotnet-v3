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
                CancellationToken.None,
                tracksDispatch: false);

            using (MemoryStream stream1 = request.CreateBodyStream())
            using (MemoryStream stream2 = request.CreateBodyStream())
            {
                Assert.AreNotSame(stream1, stream2, "Each call must return a new stream instance.");
                Assert.AreEqual(0, stream1.Position, "stream1 must be positioned at offset 0.");
                Assert.AreEqual(0, stream2.Position, "stream2 must be positioned at offset 0.");
                CollectionAssert.AreEqual(
                    stream1.ToArray(),
                    stream2.ToArray(),
                    "Both streams must contain identical serialized bytes (also implies non-empty + equal length).");
            }

            // Obtain a third stream after the first two have been disposed.
            using (MemoryStream stream3 = request.CreateBodyStream())
            {
                Assert.IsTrue(stream3.CanRead, "A stream obtained after disposing siblings must still be readable.");
                Assert.AreEqual(0, stream3.Position, "stream3 must be positioned at offset 0.");
            }
        }

        [TestMethod]
        [Description("Rotating the idempotency token installs a fresh tracker, so sticky signals from the prior token cannot leak onto the new one.")]
        public async Task RotateIdempotencyToken_ReplacesDispatchTracker()
        {
            DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None,
                tracksDispatch: true);

            Assert.IsNull(request.DispatchTracker, "No tracker is needed before the first token is generated.");

            System.Guid suppliedFirstToken = System.Guid.NewGuid();
            request.RotateIdempotencyToken(suppliedFirstToken);

            DistributedTransactionDispatchTracker firstTokenTracker = request.DispatchTracker;
            Assert.IsNotNull(firstTokenTracker);
            Assert.AreEqual(suppliedFirstToken, request.IdempotencyToken);
            Assert.AreEqual(suppliedFirstToken, firstTokenTracker.IdempotencyToken);
            firstTokenTracker.RecordDispatch("East US");
            (bool IsRetry, bool IsCrossRegionRedirect) firstTokenSignals = firstTokenTracker.RecordDispatch("West US");
            Assert.IsTrue(firstTokenSignals.IsRetry, "Test precondition: the retry signal must be set before rotation.");
            Assert.IsTrue(firstTokenSignals.IsCrossRegionRedirect, "Test precondition: the redirect signal must be set before rotation.");

            System.Guid secondToken = System.Guid.NewGuid();
            request.RotateIdempotencyToken(secondToken);

            Assert.AreEqual(secondToken, request.IdempotencyToken);
            Assert.AreNotSame(
                firstTokenTracker,
                request.DispatchTracker,
                "A rotated token must not reuse the tracker that describes its predecessor.");
            Assert.AreEqual(secondToken, request.DispatchTracker.IdempotencyToken);
            (bool IsRetry, bool IsCrossRegionRedirect) rotatedTokenSignals = request.DispatchTracker.RecordDispatch("West US");
            Assert.IsFalse(
                rotatedTokenSignals.IsRetry,
                "A rotated token has never been dispatched, so the retry signal must not carry over.");
            Assert.IsFalse(
                rotatedTokenSignals.IsCrossRegionRedirect,
                "A rotated token has no record in any region, so the redirect signal must not carry over.");
            Assert.AreEqual((true, true), firstTokenTracker.RecordDispatch("East US"),
                "Rotation must not clear the previous token's tracker.");
        }

        [TestMethod]
        [Description("Rotating the idempotency token is safe for a read transaction, which carries no dispatch tracker.")]
        public async Task RotateIdempotencyToken_WithoutTracker_DoesNotThrow()
        {
            DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None,
                tracksDispatch: false);

            Assert.IsNull(request.DispatchTracker);

            System.Guid suppliedToken = System.Guid.NewGuid();
            request.RotateIdempotencyToken(suppliedToken);

            Assert.AreEqual(suppliedToken, request.IdempotencyToken);
            Assert.IsNull(request.DispatchTracker, "Rotating a read token must not allocate a tracker.");
        }

        [TestMethod]
        [Description("Concurrent read-token rotation publishes complete immutable attempt snapshots without allocating trackers.")]
        public async Task RotateIdempotencyToken_ConcurrentReadRotation_PublishesCompleteTokens()
        {
            DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None,
                tracksDispatch: false);

            System.Guid firstToken = new System.Guid("11111111-1111-1111-1111-111111111111");
            System.Guid secondToken = new System.Guid("22222222-2222-2222-2222-222222222222");

            Parallel.For(
                fromInclusive: 0,
                toExclusive: 10000,
                body: index =>
                {
                    request.RotateIdempotencyToken(index % 2 == 0 ? firstToken : secondToken);

                    System.Guid observedToken = request.IdempotencyToken;
                    Assert.IsTrue(
                        observedToken == firstToken || observedToken == secondToken,
                        $"Observed a partially published token: {observedToken}.");
                    Assert.IsNull(request.DispatchTracker, "Read transactions must not allocate dispatch trackers.");
                });
        }

        [TestMethod]
        public async Task RotateIdempotencyToken_EmptyToken_Throws()
        {
            DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None,
                tracksDispatch: true);

            System.ArgumentException exception = Assert.ThrowsException<System.ArgumentException>(
                () => request.RotateIdempotencyToken(System.Guid.Empty));

            Assert.AreEqual("idempotencyToken", exception.ParamName);
            Assert.AreEqual(System.Guid.Empty, request.IdempotencyToken);
            Assert.IsNull(request.DispatchTracker);
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
