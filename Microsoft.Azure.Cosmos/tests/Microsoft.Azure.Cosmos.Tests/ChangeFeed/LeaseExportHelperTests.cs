//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class LeaseExportHelperTests
    {
        #region ToExportData Tests

        [TestMethod]
        [DataRow("Core", "instance1", nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease), 1, DisplayName = "Core lease with owner")]
        [DataRow("CoreEpk", "instance2", nameof(DocumentServiceLeaseVersion.EPKRangeBasedLease), 1, DisplayName = "CoreEpk lease with owner")]
        [DataRow("Core", null, nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease), 0, DisplayName = "Core lease without owner")]
        public void ToExportData_WithLeaseType_ExportsCorrectVersionAndFields(
            string leaseType,
            string owner,
            string expectedLeaseVersion,
            int expectedHistoryCount)
        {
            // Arrange
            DocumentServiceLease lease = leaseType == "CoreEpk"
                ? new DocumentServiceLeaseCoreEpk
                {
                    LeaseId = "test-lease-id",
                    LeaseToken = "0",
                    Owner = owner,
                    ContinuationToken = "continuation-token",
                    Mode = "IncrementalFeed",
                }
                : new DocumentServiceLeaseCore
                {
                    LeaseId = "test-lease-id",
                    LeaseToken = "0",
                    LeasePartitionKey = "pk-value",
                    Owner = owner,
                    ContinuationToken = "continuation-token",
                    Mode = "IncrementalFeed",
                    Properties = new Dictionary<string, string> { { "customProp", "customValue" } }
                };

            // Act
            LeaseExportData exportData = LeaseExportHelper.ToExportData(lease, "exporter");

            // Assert
            Assert.AreEqual("test-lease-id", exportData.LeaseId);
            Assert.AreEqual("0", exportData.LeaseToken);
            Assert.AreEqual(expectedLeaseVersion, exportData.LeaseVersion);
            Assert.AreEqual("exporter", exportData.ExportedBy);
            Assert.AreEqual(expectedHistoryCount, exportData.OwnershipHistory.Count);

            if (expectedHistoryCount > 0)
            {
                Assert.AreEqual(owner, exportData.OwnershipHistory[0].Owner);
                Assert.AreEqual("exported", exportData.OwnershipHistory[0].Action);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToExportData_WithNullLease_ThrowsArgumentNullException()
        {
            LeaseExportHelper.ToExportData(null, "exporter");
        }

        #endregion

        #region FromExportData Tests

        [TestMethod]
        [DataRow(nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease), "DocumentServiceLeaseCore", DisplayName = "PKRange creates Core")]
        [DataRow(nameof(DocumentServiceLeaseVersion.EPKRangeBasedLease), "DocumentServiceLeaseCoreEpk", DisplayName = "EPK creates CoreEpk")]
        public void FromExportData_WithLeaseVersion_CreatesCorrectType(
            string leaseVersion,
            string expectedTypeName)
        {
            // Arrange
            LeaseExportData exportData = new LeaseExportData
            {
                LeaseId = "imported-lease-id",
                LeaseToken = "2",
                PartitionKey = "imported-pk",
                Owner = "old-owner",
                ContinuationToken = "imported-continuation",
                Mode = "IncrementalFeed",
                LeaseVersion = leaseVersion,
                Properties = new Dictionary<string, string> { { "key", "value" } },
                ExportedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                ExportedBy = "original-exporter",
            };

            // Act
            DocumentServiceLease lease = LeaseExportHelper.FromExportData(exportData, "new-importer");

            // Assert
            Assert.AreEqual(expectedTypeName, lease.GetType().Name);
            Assert.AreEqual("imported-lease-id", lease.Id);
            Assert.AreEqual("2", lease.CurrentLeaseToken);
            Assert.AreEqual("new-importer", lease.Owner);
            Assert.AreEqual("imported-continuation", lease.ContinuationToken);
            Assert.AreEqual("IncrementalFeed", lease.Mode);
            Assert.AreEqual("new-importer", lease.Properties["_importedBy"]);
            Assert.AreEqual("old-owner", lease.Properties["_importedFromOwner"]);
        }

        [TestMethod]
        public void FromExportData_PreservesOwnershipHistory()
        {
            // Arrange
            LeaseExportData exportData = new LeaseExportData
            {
                LeaseId = "lease-with-history",
                LeaseToken = "3",
                LeaseVersion = nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease),
                OwnershipHistory = new List<LeaseOwnershipHistory>
                {
                    new LeaseOwnershipHistory("owner1", new DateTime(2026, 1, 1), "acquired"),
                    new LeaseOwnershipHistory("owner2", new DateTime(2026, 1, 5), "exported"),
                }
            };

            // Act
            DocumentServiceLease lease = LeaseExportHelper.FromExportData(exportData, "owner3");

            // Assert
            Assert.IsTrue(lease.Properties.ContainsKey("_ownershipHistory"));
            string historyJson = lease.Properties["_ownershipHistory"];
            List<LeaseOwnershipHistory> history = JsonSerializer.Deserialize<List<LeaseOwnershipHistory>>(historyJson);
            Assert.AreEqual(3, history.Count); // 2 original + 1 for import
            Assert.AreEqual("owner3", history[2].Owner);
            Assert.AreEqual("imported", history[2].Action);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void FromExportData_WithNullExportData_ThrowsArgumentNullException()
        {
            LeaseExportHelper.FromExportData(null, "importer");
        }

        #endregion

        #region GetOwnershipHistory Tests

        [TestMethod]
        public void GetOwnershipHistory_WithValidHistory_ReturnsHistory()
        {
            // Arrange
            List<LeaseOwnershipHistory> expectedHistory = new List<LeaseOwnershipHistory>
            {
                new LeaseOwnershipHistory("owner1", new DateTime(2026, 1, 1), "acquired"),
            };
            string historyJson = JsonSerializer.Serialize(expectedHistory);

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore
            {
                LeaseId = "test",
                Properties = new Dictionary<string, string>
                {
                    { "_ownershipHistory", historyJson }
                }
            };

            // Act
            List<LeaseOwnershipHistory> result = LeaseExportHelper.GetOwnershipHistory(lease);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("owner1", result[0].Owner);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "No history property")]
        [DataRow("", DisplayName = "Empty string")]
        [DataRow("invalid-json", DisplayName = "Invalid JSON")]
        public void GetOwnershipHistory_WithMissingOrInvalidHistory_ReturnsEmptyList(string historyValue)
        {
            // Arrange
            Dictionary<string, string> properties = new Dictionary<string, string>();
            if (historyValue != null)
            {
                properties["_ownershipHistory"] = historyValue;
            }

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore
            {
                LeaseId = "test",
                Properties = properties
            };

            // Act
            List<LeaseOwnershipHistory> result = LeaseExportHelper.GetOwnershipHistory(lease);

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region Serialization Tests

        [TestMethod]
        public void LeaseExportData_CanSerializeAndDeserialize()
        {
            // Arrange
            LeaseExportData original = new LeaseExportData
            {
                LeaseId = "serialize-test",
                LeaseToken = "4",
                Owner = "test-owner",
                ContinuationToken = "test-continuation",
                Timestamp = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                Mode = "IncrementalFeed",
                LeaseVersion = nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease),
                ExportedAt = new DateTime(2026, 1, 15, 13, 0, 0, DateTimeKind.Utc),
                ExportedBy = "serialization-test",
                Properties = new Dictionary<string, string> { { "key", "value" } },
                OwnershipHistory = new List<LeaseOwnershipHistory>
                {
                    new LeaseOwnershipHistory("owner1", new DateTime(2026, 1, 10), "acquired"),
                }
            };

            // Act
            string json = JsonSerializer.Serialize(original);
            LeaseExportData deserialized = JsonSerializer.Deserialize<LeaseExportData>(json);

            // Assert
            Assert.AreEqual(original.LeaseId, deserialized.LeaseId);
            Assert.AreEqual(original.LeaseToken, deserialized.LeaseToken);
            Assert.AreEqual(original.Owner, deserialized.Owner);
            Assert.AreEqual(original.ContinuationToken, deserialized.ContinuationToken);
            Assert.AreEqual(original.Mode, deserialized.Mode);
            Assert.AreEqual(original.LeaseVersion, deserialized.LeaseVersion);
            Assert.AreEqual(original.ExportedBy, deserialized.ExportedBy);
            Assert.AreEqual(1, deserialized.OwnershipHistory.Count);
            Assert.AreEqual("owner1", deserialized.OwnershipHistory[0].Owner);
        }

        #endregion
    }
}
