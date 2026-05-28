// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using CosmosPK = Microsoft.Azure.Cosmos.PartitionKey;

    [TestClass]
    public class DistributedReadTransactionCoreTests
    {
        private static readonly string Database = "testDb";
        private static readonly string Collection = "testColl";
        private static readonly CosmosPK TestPartitionKey = new CosmosPK("pk1");
        private static readonly string ItemId = "item-1";

        private DistributedReadTransactionCore CreateTransaction()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            return new DistributedReadTransactionCore(mockContext.Object);
        }

        #region Constructor validation

        [TestMethod]
        public void Constructor_NullClientContext_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new DistributedReadTransactionCore(null));
        }

        #endregion

        #region ReadItem argument validation

        [TestMethod]
        public void ReadItem_NullDatabase_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(null, Collection, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_EmptyDatabase_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(string.Empty, Collection, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_WhitespaceDatabase_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem("   ", Collection, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_NullCollection_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(Database, null, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_EmptyCollection_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(Database, string.Empty, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_NullId_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(Database, Collection, TestPartitionKey, null));
        }

        [TestMethod]
        public void ReadItem_EmptyId_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(Database, Collection, TestPartitionKey, string.Empty));
        }

        [TestMethod]
        public void ReadItem_WhitespaceId_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(Database, Collection, TestPartitionKey, "   "));
        }

        #endregion

        #region Operation building

        [TestMethod]
        public void ReadItem_ValidArgs_ReturnsThisForChaining()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            DistributedReadTransaction result = txn.ReadItem(Database, Collection, TestPartitionKey, ItemId);
            Assert.AreSame(txn, result);
        }

        [TestMethod]
        public void ReadItem_BuildsOperationWithReadType()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(Database, Collection, TestPartitionKey, ItemId);

            IReadOnlyList<DistributedTransactionOperation> ops = GetOperations(txn);
            Assert.AreEqual(1, ops.Count);
            Assert.AreEqual(OperationType.Read, ops[0].OperationType);
        }

        [TestMethod]
        public void ReadItem_BuildsOperationWithCorrectFields()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(Database, Collection, TestPartitionKey, ItemId);

            DistributedTransactionOperation op = GetOperations(txn)[0];
            Assert.AreEqual(Database, op.Database);
            Assert.AreEqual(Collection, op.Container);
            Assert.AreEqual(ItemId, op.Id);
            Assert.AreEqual(0, op.OperationIndex);
        }

        [TestMethod]
        public void ReadItem_HasNoResourceBody()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(Database, Collection, TestPartitionKey, ItemId);

            DistributedTransactionOperation op = GetOperations(txn)[0];
            Assert.IsTrue(op.ResourceBody.IsEmpty);
            Assert.IsNull(op.ResourceStream);
        }

        [TestMethod]
        public void MultipleReadItems_CorrectOperationIndices()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(Database, Collection, TestPartitionKey, "id-0")
                .ReadItem(Database, Collection, TestPartitionKey, "id-1")
                .ReadItem(Database, Collection, TestPartitionKey, "id-2");

            IReadOnlyList<DistributedTransactionOperation> ops = GetOperations(txn);
            Assert.AreEqual(3, ops.Count);
            Assert.AreEqual(0, ops[0].OperationIndex);
            Assert.AreEqual(1, ops[1].OperationIndex);
            Assert.AreEqual(2, ops[2].OperationIndex);
        }

        [TestMethod]
        public void ReadItem_WithRequestOptions_SetsOptionsOnOperation()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            DistributedTransactionRequestOptions options = new DistributedTransactionRequestOptions();
            txn.ReadItem(Database, Collection, TestPartitionKey, ItemId, options);

            DistributedTransactionOperation op = GetOperations(txn)[0];
            Assert.AreSame(options, op.RequestOptions);
        }

        #endregion

        #region OperationHelperAsync wiring

        [TestMethod]
        [Description("Verifies CommitTransactionAsync routes through OperationHelperAsync with the qualified operationName, the read-specific OTel constant, the CommitDistributedTransaction operation type, and TraceComponent.Batch.")]
        public async Task CommitTransactionAsync_RoutesThroughOperationHelper_WithExpectedWiring()
        {
            string capturedOperationName = null;
            OperationType capturedOperationType = default;
            TraceComponent capturedTraceComponent = default;
            string capturedOTelOperationName = null;

            Mock<CosmosClientContext> contextMock = new Mock<CosmosClientContext>();
            contextMock
                .Setup(c => c.OperationHelperAsync<DistributedTransactionResponse>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<Func<ITrace, Task<DistributedTransactionResponse>>>(),
                    It.IsAny<(string OperationName, Func<DistributedTransactionResponse, OpenTelemetryAttributes> GetAttributes)?>(),
                    It.IsAny<ResourceType?>(),
                    It.IsAny<TraceComponent>(),
                    It.IsAny<TraceLevel>()))
                .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<DistributedTransactionResponse>>, (string, Func<DistributedTransactionResponse, OpenTelemetryAttributes>)?, ResourceType?, TraceComponent, TraceLevel>(
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelTuple, resourceType, comp, level) =>
                    {
                        capturedOperationName = operationName;
                        capturedOperationType = operationType;
                        capturedTraceComponent = comp;
                        capturedOTelOperationName = oTelTuple?.Item1;
                        return Task.FromResult<DistributedTransactionResponse>(null);
                    });

            DistributedReadTransactionCore txn = new DistributedReadTransactionCore(contextMock.Object);
            txn.ReadItem(Database, Collection, TestPartitionKey, ItemId);

            await txn.CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual($"{nameof(DistributedReadTransaction)}.{nameof(DistributedReadTransaction.CommitTransactionAsync)}", capturedOperationName);
            Assert.AreEqual(OperationType.CommitDistributedTransaction, capturedOperationType);
            Assert.AreEqual(TraceComponent.Batch, capturedTraceComponent);
            Assert.AreEqual(OpenTelemetryConstants.Operations.CommitDistributedReadTransaction, capturedOTelOperationName);
        }

        #endregion

        /// <summary>
        /// Reflective helper to access the private operations list for assertion purposes.
        /// </summary>
        private static IReadOnlyList<DistributedTransactionOperation> GetOperations(DistributedReadTransactionCore txn)
        {
            System.Reflection.FieldInfo field = typeof(DistributedReadTransactionCore)
                .GetField("operations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (IReadOnlyList<DistributedTransactionOperation>)field.GetValue(txn);
        }
    }
}
