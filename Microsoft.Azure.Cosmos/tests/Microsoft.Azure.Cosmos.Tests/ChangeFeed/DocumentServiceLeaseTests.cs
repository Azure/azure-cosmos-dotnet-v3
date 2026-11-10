//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Serialization.Formatters.Binary;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void PreFeatureLeaseSchemaDropsQueryIdentityOnReplacement(bool epk)
        {
            DocumentServiceLease original = epk
                ? new DocumentServiceLeaseCoreEpk { LeaseId = "lease" }
                : new DocumentServiceLeaseCore { LeaseId = "lease", LeaseToken = "0" };
            original.QueryFilterHash = ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec("SELECT * FROM c"));
            original.ContinuationToken = "saved-progress";
            JsonSerializerSettings preFeatureSchema = new JsonSerializerSettings
            {
                ContractResolver = new PreFeatureLeaseContractResolver()
            };
            DocumentServiceLease oldHostLease = JsonConvert.DeserializeObject<DocumentServiceLease>(
                JsonConvert.SerializeObject(original), preFeatureSchema);
            Assert.IsNull(oldHostLease.QueryFilterHash);
            oldHostLease.Owner = "old-host";
            string replacement = JsonConvert.SerializeObject(oldHostLease, preFeatureSchema);
            Assert.IsNull(JObject.Parse(replacement).Property("QueryFilterHash"));
            DocumentServiceLease reread = JsonConvert.DeserializeObject<DocumentServiceLease>(replacement);
            Assert.AreEqual("saved-progress", reread.ContinuationToken);
            Assert.AreEqual("old-host", reread.Owner);
            Assert.IsNull(reread.QueryFilterHash);
        }

        private sealed class PreFeatureLeaseContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                // Baseline 3a0655e lease types have no query field or extension-data preservation.
                // Model that serializer contract without deleting fields from the input JSON.
                if (member.Name == nameof(DocumentServiceLease.QueryFilterHash))
                {
                    property.Ignored = true;
                }

                return property;
            }
        }

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

        [DataTestMethod]
        [DataRow(false, null)]
        [DataRow(true, null)]
        [DataRow(false, "")]
        [DataRow(true, "")]
        [DataRow(false, "fingerprint")]
        [DataRow(true, "fingerprint")]
        public void QueryFingerprintRoundTripsThroughLeaseConverter(bool epkLease, string hash)
        {
            DocumentServiceLease original = epkLease
                ? new DocumentServiceLeaseCoreEpk()
                : new DocumentServiceLeaseCore();
            original.QueryFilterHash = hash;

            string serialized = JsonConvert.SerializeObject(original);
            JObject document = JObject.Parse(serialized);
            if (hash == null)
            {
                Assert.IsNull(document.Property("QueryFilterHash"));
            }
            else
            {
                Assert.AreEqual(hash, (string)document["QueryFilterHash"]);
            }

            DocumentServiceLease restored = JsonConvert.DeserializeObject<DocumentServiceLease>(serialized);
            Assert.AreEqual(original.GetType(), restored.GetType());
            Assert.AreEqual(hash, restored.QueryFilterHash);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ExplicitNullFingerprintRemainsUnfiltered(bool epkLease)
        {
            DocumentServiceLease original = epkLease
                ? new DocumentServiceLeaseCoreEpk()
                : new DocumentServiceLeaseCore();
            JObject document = JObject.FromObject(original);
            document["QueryFilterHash"] = JValue.CreateNull();

            DocumentServiceLease restored = JsonConvert.DeserializeObject<DocumentServiceLease>(document.ToString());
            Assert.AreEqual(original.GetType(), restored.GetType());
            Assert.IsNull(restored.QueryFilterHash);
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