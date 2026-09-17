// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.IO;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;

    [TestClass]
    public class DistributedTransactionOperationTests
    {
        [TestMethod]
        public void Constructor_PreservesMetadataAndPayloadReferences()
        {
            PartitionKey partitionKey = new PartitionKey("partition");
            object resource = new object();
            using MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3 });
            stream.Position = 1;
            DistributedTransactionOperation[] operations =
            {
                new DistributedTransactionOperation(OperationType.Read, 0, "db", "container", partitionKey, "read"),
                new DistributedTransactionOperation(OperationType.Create, 1, "db", "container", partitionKey, "stream")
                {
                    ResourceStream = stream
                },
                new DistributedTransactionOperation<object>(OperationType.Upsert, 2, "db", "container", partitionKey, "typed", resource)
            };

            for (int i = 0; i < operations.Length; i++)
            {
                Assert.AreEqual(i, operations[i].OperationIndex);
                Assert.AreEqual("db", operations[i].Database);
                Assert.AreEqual("container", operations[i].Container);
                Assert.AreEqual(partitionKey, operations[i].PartitionKey);
                Assert.IsNull(operations[i].RequestOptions);
                Assert.IsNull(operations[i].SessionToken);
                Assert.IsNull(operations[i].IfMatch);
                Assert.IsNull(operations[i].IfNoneMatch);
                Assert.IsTrue(operations[i].ResourceBody.IsEmpty);
            }

            Assert.AreEqual("read", operations[0].Id);
            Assert.AreEqual(OperationType.Read, operations[0].OperationType);
            Assert.IsNull(operations[0].ResourceStream);
            Assert.AreEqual("stream", operations[1].Id);
            Assert.AreEqual(OperationType.Create, operations[1].OperationType);
            Assert.AreSame(stream, operations[1].ResourceStream);
            Assert.AreEqual(1L, stream.Position);
            Assert.IsTrue(stream.CanRead);
            Assert.AreEqual("typed", operations[2].Id);
            Assert.AreEqual(OperationType.Upsert, operations[2].OperationType);
            Assert.AreSame(resource, ((DistributedTransactionOperation<object>)operations[2]).Resource);
            Assert.IsNull(operations[2].ResourceStream);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Constructor_RetainsOptionsAndPreservesSubtype(bool typed)
        {
            DistributedTransactionPatchItemRequestOptions options = new DistributedTransactionPatchItemRequestOptions
            {
                IfMatchEtag = "match",
                IfNoneMatchEtag = "nonmatch",
                SessionToken = "0:-1#123",
                FilterPredicate = "from c where c.value = 1"
            };
            DistributedTransactionOperation operation = typed
                ? new DistributedTransactionOperation<object>(OperationType.Patch, 0, "db", "container", new PartitionKey("pk"), "id", new object(), options)
                : new DistributedTransactionOperation(OperationType.Read, 0, "db", "container", new PartitionKey("pk"), "id", options);

            Assert.AreSame(options, operation.RequestOptions);
            Assert.IsInstanceOfType(operation.RequestOptions, typeof(DistributedTransactionPatchItemRequestOptions));
            options.IfMatchEtag = "changed";
            options.IfNoneMatchEtag = null;
            options.SessionToken = "0:-1#456";
            options.FilterPredicate = null;

            Assert.AreEqual("changed", operation.IfMatch);
            Assert.IsNull(operation.IfNoneMatch);
            Assert.AreEqual("0:-1#456", operation.SessionToken);
            Assert.IsNull(
                ((DistributedTransactionPatchItemRequestOptions)operation.RequestOptions).FilterPredicate);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void Constructor_SessionToken_IsNormalizedWhenRead(string sessionToken)
        {
            DistributedTransactionRequestOptions options = new DistributedTransactionRequestOptions { SessionToken = sessionToken };
            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Read, 0, "db", "container", new PartitionKey("pk"), "id", options);

            Assert.IsNull(operation.SessionToken);
            Assert.AreEqual(sessionToken, operation.RequestOptions.SessionToken);
            options.SessionToken = "0:-1#123";
            Assert.AreEqual("0:-1#123", operation.SessionToken);
        }
    }
}
