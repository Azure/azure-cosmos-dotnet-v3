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
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseTests
    {
        [TestMethod]
        public void ValidateProperties()
        {
            string id = "id";
            string etag = "etag";
            string partitionId = "0";
            string owner = "owner";
            string continuationToken = "continuation";
            DateTime timestamp = DateTime.Now - TimeSpan.FromSeconds(5);
            string key = "key";
            string value = "value";

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
                LeasePartitionKey = "pk",
                Timestamp = DateTime.Now - TimeSpan.FromSeconds(5),
                Properties = new Dictionary<string, string> { { "key", "value" } }
            };

            string json = JsonConvert.SerializeObject(originalLease);
            DocumentServiceLeaseCore lease = JsonConvert.DeserializeObject<DocumentServiceLeaseCore>(json);

            Assert.AreEqual(originalLease.Id, lease.Id);
            Assert.AreEqual(originalLease.ETag, lease.ETag);
            Assert.AreEqual(originalLease.LeaseToken, lease.LeaseToken);
            Assert.AreEqual(originalLease.Owner, lease.Owner);
            Assert.AreEqual(originalLease.ContinuationToken, lease.ContinuationToken);
            Assert.AreEqual(originalLease.Timestamp, lease.Timestamp);
            Assert.AreEqual(originalLease.PartitionKey, lease.PartitionKey);
            Assert.AreEqual(originalLease.Properties["key"], lease.Properties["key"]);
        }

        // Make sure that when some fields are not set, serialization is not broken.
        [TestMethod]
        public void ValidateSerialization_NullFields()
        {
            DocumentServiceLeaseCore originalLease = new DocumentServiceLeaseCore();
            string json = JsonConvert.SerializeObject(originalLease);
            DocumentServiceLeaseCore lease = JsonConvert.DeserializeObject<DocumentServiceLeaseCore>(json);

            Assert.IsNull(lease.Id);
            Assert.IsNull(lease.ETag);
            Assert.IsNull(lease.LeaseToken);
            Assert.IsNull(lease.Owner);
            Assert.IsNull(lease.ContinuationToken);
            Assert.IsNull(lease.PartitionKey);
            Assert.AreEqual(new DocumentServiceLeaseCore().Timestamp, lease.Timestamp);
            Assert.IsTrue(lease.Properties.Count == 0);
        }

        [TestMethod]
        public void ValidateJsonSerialization_PKRangeLease()
        {
            DocumentServiceLeaseCore originalLease = new DocumentServiceLeaseCore
            {
                LeaseId = "id",
                ETag = "etag",
                LeaseToken = "0",
                Owner = "owner",
                ContinuationToken = "continuation",
                Timestamp = DateTime.Now - TimeSpan.FromSeconds(5),
                LeasePartitionKey = "partitionKey",
                Properties = new Dictionary<string, string> { { "key", "value" } },
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            string serialized = JsonConvert.SerializeObject(originalLease);

            DocumentServiceLease documentServiceLease = JsonConvert.DeserializeObject<DocumentServiceLease>(serialized);

            if (documentServiceLease is DocumentServiceLeaseCore documentServiceLeaseCore)
            {
                Assert.AreEqual(originalLease.LeaseId, documentServiceLeaseCore.LeaseId);
                Assert.AreEqual(originalLease.ETag, documentServiceLeaseCore.ETag);
                Assert.AreEqual(originalLease.LeaseToken, documentServiceLeaseCore.LeaseToken);
                Assert.AreEqual(originalLease.Owner, documentServiceLeaseCore.Owner);
                Assert.AreEqual(originalLease.PartitionKey, documentServiceLeaseCore.PartitionKey);
                Assert.AreEqual(originalLease.ContinuationToken, documentServiceLeaseCore.ContinuationToken);
                Assert.AreEqual(originalLease.Timestamp, documentServiceLeaseCore.Timestamp);
                Assert.AreEqual(originalLease.Properties["key"], documentServiceLeaseCore.Properties["key"]);
                Assert.AreEqual(originalLease.FeedRange.ToJsonString(), documentServiceLeaseCore.FeedRange.ToJsonString());
            }
            else
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void ValidateJsonSerialization_EPKLease()
        {
            DocumentServiceLeaseCoreEpk originalLease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "id",
                ETag = "etag",
                LeaseToken = "0",
                Owner = "owner",
                ContinuationToken = "continuation",
                Timestamp = DateTime.Now - TimeSpan.FromSeconds(5),
                LeasePartitionKey = "partitionKey",
                Properties = new Dictionary<string, string> { { "key", "value" } },
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false))
            };

            string serialized = JsonConvert.SerializeObject(originalLease);

            DocumentServiceLease documentServiceLease = JsonConvert.DeserializeObject<DocumentServiceLease>(serialized);

            if (documentServiceLease is DocumentServiceLeaseCoreEpk documentServiceLeaseCore)
            {
                Assert.AreEqual(originalLease.LeaseId, documentServiceLeaseCore.LeaseId);
                Assert.AreEqual(originalLease.ETag, documentServiceLeaseCore.ETag);
                Assert.AreEqual(originalLease.LeaseToken, documentServiceLeaseCore.LeaseToken);
                Assert.AreEqual(originalLease.Owner, documentServiceLeaseCore.Owner);
                Assert.AreEqual(originalLease.PartitionKey, documentServiceLeaseCore.PartitionKey);
                Assert.AreEqual(originalLease.ContinuationToken, documentServiceLeaseCore.ContinuationToken);
                Assert.AreEqual(originalLease.Timestamp, documentServiceLeaseCore.Timestamp);
                Assert.AreEqual(originalLease.Properties["key"], documentServiceLeaseCore.Properties["key"]);
                Assert.AreEqual(originalLease.FeedRange.ToJsonString(), documentServiceLeaseCore.FeedRange.ToJsonString());
            }
            else
            {
                Assert.Fail();
            }
        }
    }
}