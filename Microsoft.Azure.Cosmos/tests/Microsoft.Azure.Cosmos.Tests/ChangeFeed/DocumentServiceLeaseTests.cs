//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DocumentServiceLeaseTests
    {
        [TestMethod]
        public void ValidateProperties()
        {
            var id = "id";
            var etag = "etag";
            var partitionId = "0";
            var owner = "owner";
            var continuationToken = "continuation";
            var timestamp = DateTime.Now - TimeSpan.FromSeconds(5);
            var key = "key";
            var value = "value";

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore
            {
                LeaseId = id,
                ETag = etag,
                LeaseToken = partitionId,
                Owner = owner,
                ContinuationToken = continuationToken,
                Timestamp = timestamp,
                Properties = new Dictionary<string, string> { { "key", "value" } },
            };

            Assert.AreEqual(id, lease.Id);
            Assert.AreEqual(etag, lease.ETag);
            Assert.AreEqual(partitionId, lease.LeaseToken);
            Assert.AreEqual(owner, lease.Owner);
            Assert.AreEqual(continuationToken, lease.ContinuationToken);
            Assert.AreEqual(timestamp, lease.Timestamp);
            Assert.AreEqual(value, lease.Properties[key]);
            Assert.AreEqual(etag, lease.ConcurrencyToken);
        }

        [TestMethod]
        public void ValidateSerialization_AllFields()
        {
            DocumentServiceLeaseCore originalLease = new DocumentServiceLeaseCore
            {
                LeaseId = "id",
                ETag = "etag",
                LeaseToken = "0",
                Owner = "owner",
                ContinuationToken = "continuation",
                Timestamp = DateTime.Now - TimeSpan.FromSeconds(5),
                Properties = new Dictionary<string, string> { { "key", "value" } }
            };

            var buffer = new byte[4096];
            var formatter = new BinaryFormatter();
            var stream1 = new MemoryStream(buffer);
            var stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalLease);
            var lease = (DocumentServiceLeaseCore)formatter.Deserialize(stream2);

            Assert.AreEqual(originalLease.Id, lease.Id);
            Assert.AreEqual(originalLease.ETag, lease.ETag);
            Assert.AreEqual(originalLease.LeaseToken, lease.LeaseToken);
            Assert.AreEqual(originalLease.Owner, lease.Owner);
            Assert.AreEqual(originalLease.ContinuationToken, lease.ContinuationToken);
            Assert.AreEqual(originalLease.Timestamp, lease.Timestamp);
            Assert.AreEqual(originalLease.Properties["key"], lease.Properties["key"]);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            DocumentServiceLeaseCore originalLease = new DocumentServiceLeaseCore();
            var buffer = new byte[4096];
            var formatter = new BinaryFormatter();
            var stream1 = new MemoryStream(buffer);
            var stream2 = new MemoryStream(buffer);

            formatter.Serialize(stream1, originalLease);
            var lease = (DocumentServiceLeaseCore)formatter.Deserialize(stream2);

            Assert.IsNull(lease.Id);
            Assert.IsNull(lease.ETag);
            Assert.IsNull(lease.LeaseToken);
            Assert.IsNull(lease.Owner);
            Assert.IsNull(lease.ContinuationToken);
            Assert.AreEqual(new DocumentServiceLeaseCore().Timestamp, lease.Timestamp);
            Assert.IsTrue(lease.Properties.Count == 0);
        }
    }
}
