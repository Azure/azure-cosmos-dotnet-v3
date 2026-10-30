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
    using Newtonsoft.Json.Linq;

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

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, false, true)]
        [DataRow(false, true, false)]
        [DataRow(false, true, true)]
        [DataRow(true, false, false)]
        [DataRow(true, false, true)]
        public void ClonePreservesPersistedSchema(bool useEpkLease, bool legacySchema, bool explicitTimestamp)
        {
            JObject document = JObject.Parse(@"{
                'id': 'lease',
                'partitionKey': 'pk',
                '_etag': 'etag',
                'LeaseToken': '0',
                'Owner': 'host',
                'ContinuationToken': 'continuation',
                '_ts': 12345,
                'Mode': 'Incremental feed',
                'properties': { 'key': 'value' },
                'FeedRange': { 'Range': { 'min': 'AA', 'max': 'BB', 'isMaxInclusive': true } }
            }");
            if (legacySchema)
            {
                document.Remove("LeaseToken");
                document["PartitionId"] = "0";
            }
            else
            {
                document["version"] = (int)(useEpkLease
                    ? DocumentServiceLeaseVersion.EPKRangeBasedLease
                    : DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease);
            }

            if (explicitTimestamp)
            {
                document["timestamp"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            }

            DocumentServiceLease lease = JsonConvert.DeserializeObject<DocumentServiceLease>(document.ToString());
            DocumentServiceLease clone = lease.Clone();

            Assert.AreEqual(lease.GetType(), clone.GetType());
            Assert.AreNotSame(lease, clone);
            Assert.AreEqual(lease.Timestamp, clone.Timestamp);
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(lease), JObject.FromObject(clone)));

            clone.ConcurrencyToken = "new etag";
            JObject serialized = JObject.FromObject(clone);
            Assert.AreEqual("etag", lease.ConcurrencyToken);
            Assert.AreEqual("new etag", (string)serialized["_etag"]);
            Assert.IsNull(serialized["ConcurrencyToken"]);
            Assert.AreEqual(legacySchema ? "0" : null, (string)serialized["PartitionId"]);
            Assert.AreEqual(12345L, (long)serialized["_ts"]);
            Assert.AreEqual("pk", (string)serialized["partitionKey"]);
            Assert.AreEqual("Incremental feed", (string)serialized["Mode"]);
            Assert.AreEqual("value", (string)serialized["properties"]["key"]);
            Assert.IsTrue(((FeedRangeEpk)clone.FeedRange).Range.IsMaxInclusive);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ClonePreservesNullFields(bool useEpkLease)
        {
            DocumentServiceLease lease = useEpkLease ? new DocumentServiceLeaseCoreEpk() : new DocumentServiceLeaseCore();
            lease.Properties = null;
            DocumentServiceLease clone = lease.Clone();

            Assert.IsNull(clone.Properties);
            Assert.IsNull(clone.FeedRange);
            Assert.IsNull(clone.ConcurrencyToken);
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(lease), JObject.FromObject(clone)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ClonePreservesPropertiesComparerAndFeedRangeValues(bool useEpkLease)
        {
            FeedRangeInternal[] feedRanges =
            {
                null,
                new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)),
                new FeedRangePartitionKeyRange("0"),
                new FeedRangePartitionKey(new PartitionKey("pk")),
                new FeedRangePartitionKey(PartitionKey.None)
            };

            foreach (FeedRangeInternal feedRange in feedRanges)
            {
                DocumentServiceLease lease = useEpkLease ? new DocumentServiceLeaseCoreEpk() : new DocumentServiceLeaseCore();
                lease.FeedRange = feedRange;
                lease.Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["key"] = "value" };
                DocumentServiceLease clone = lease.Clone();

                Assert.AreNotSame(lease.Properties, clone.Properties);
                Assert.AreSame(lease.Properties.Comparer, clone.Properties.Comparer);
                Assert.AreEqual("value", clone.Properties["KEY"]);
                clone.Properties["KEY"] = "changed";
                Assert.AreEqual("value", lease.Properties["key"]);
                Assert.AreEqual(feedRange?.ToJsonString(), clone.FeedRange?.ToJsonString());
                if (feedRange is FeedRangeEpk epk)
                {
                    Assert.AreNotSame(epk, clone.FeedRange);
                    Assert.AreNotSame(epk.Range, ((FeedRangeEpk)clone.FeedRange).Range);
                }
            }
        }
    }
}