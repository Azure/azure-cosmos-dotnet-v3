// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="DistributedTransactionConstants.IsDistributedTransactionRequest"/>.
    ///
    /// This detector is the single source of truth that every DTX guard relies on
    /// (region routing in <see cref="ClientRetryPolicy"/> / <see cref="LocationCache"/>, the
    /// default HTTP timeout policy, and the POST-method classification). A regression here
    /// would silently re-enable read-region routing for read transactions, so the truth
    /// table is pinned down explicitly.
    ///
    /// Note: <see cref="OperationType"/> / <see cref="ResourceType"/> are internal, so the
    /// scenarios are iterated inside each test rather than supplied via public [DataRow] params.
    /// </summary>
    [TestClass]
    public class DistributedTransactionConstantsTests
    {
        [TestMethod]
        public void IsDistributedTransactionRequest_ReadOnDistributedTransactionBatch_ReturnsTrue()
        {
            // DistributedReadTransaction dispatches as OperationType.Read on the wire.
            Assert.IsTrue(DistributedTransactionConstants.IsDistributedTransactionRequest(
                OperationType.Read,
                ResourceType.DistributedTransactionBatch));
        }

        [TestMethod]
        public void IsDistributedTransactionRequest_CommitOnDistributedTransactionBatch_ReturnsTrue()
        {
            // DistributedWriteTransaction dispatches as OperationType.CommitDistributedTransaction.
            Assert.IsTrue(DistributedTransactionConstants.IsDistributedTransactionRequest(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch));
        }

        [TestMethod]
        public void IsDistributedTransactionRequest_ReadOnNonDtxResource_ReturnsFalse()
        {
            // A plain point read (Read + Document) must NOT be classified as a DTX request,
            // otherwise normal reads would be forced onto the write region.
            ResourceType[] nonDtxResources = new[]
            {
                ResourceType.Document,
                ResourceType.Collection,
                ResourceType.Database,
                ResourceType.PartitionKeyRange,
            };

            foreach (ResourceType resourceType in nonDtxResources)
            {
                Assert.IsFalse(
                    DistributedTransactionConstants.IsDistributedTransactionRequest(OperationType.Read, resourceType),
                    $"Read on {resourceType} must not be a DTX request.");
            }
        }

        [TestMethod]
        public void IsDistributedTransactionRequest_NonDtxOperationOnDtxResource_ReturnsFalse()
        {
            // Only Read and CommitDistributedTransaction are DTX operations on the DTX resource.
            OperationType[] nonDtxOperations = new[]
            {
                OperationType.Create,
                OperationType.Replace,
                OperationType.Upsert,
                OperationType.Delete,
                OperationType.Patch,
                OperationType.Query,
                OperationType.Batch,
            };

            foreach (OperationType operationType in nonDtxOperations)
            {
                Assert.IsFalse(
                    DistributedTransactionConstants.IsDistributedTransactionRequest(operationType, ResourceType.DistributedTransactionBatch),
                    $"{operationType} on DistributedTransactionBatch must not be a DTX request.");
            }
        }

        [TestMethod]
        public void IsDistributedTransactionRequest_NonDtxOperationAndResource_ReturnsFalse()
        {
            Assert.IsFalse(DistributedTransactionConstants.IsDistributedTransactionRequest(OperationType.Create, ResourceType.Document));
            Assert.IsFalse(DistributedTransactionConstants.IsDistributedTransactionRequest(OperationType.Query, ResourceType.Document));
            Assert.IsFalse(DistributedTransactionConstants.IsDistributedTransactionRequest(OperationType.Read, ResourceType.Collection));
            Assert.IsFalse(DistributedTransactionConstants.IsDistributedTransactionRequest(OperationType.Delete, ResourceType.Database));
        }
    }
}
