//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseContainerInMemoryTests
    {
        [TestMethod]
        public async Task AllLeasesAreOwnedLeases()
        {
            List<DocumentServiceLease> expectedLeases = new List<DocumentServiceLease>()
            {
                Mock.Of<DocumentServiceLease>()
            };
            ConcurrentDictionary<string, DocumentServiceLease> concurrentDictionary = new ConcurrentDictionary<string, DocumentServiceLease>();
            concurrentDictionary.TryAdd("0", expectedLeases.First());
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(concurrentDictionary);
            IEnumerable<DocumentServiceLease> ownedLeases = await inMemoryContainer.GetOwnedLeasesAsync();
            IEnumerable<DocumentServiceLease> allLeases = await inMemoryContainer.GetAllLeasesAsync();
            CollectionAssert.AreEqual(expectedLeases, ownedLeases.ToList());
            CollectionAssert.AreEqual(allLeases.ToList(), ownedLeases.ToList());
        }

        #region ExportLeasesAsync Tests

        [TestMethod]
        [DataRow(0, DisplayName = "Empty container returns empty list")]
        [DataRow(2, DisplayName = "Container with two leases returns both")]
        public async Task ExportLeasesAsync_ReturnsExpectedCount(int leaseCount)
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            for (int i = 0; i < leaseCount; i++)
            {
                DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore
                {
                    LeaseId = $"lease{i}",
                    LeaseToken = i.ToString(),
                    Owner = $"instance{i}"
                };
                container.TryAdd(lease.Id, lease);
            }

            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container);

            // Act
            IReadOnlyList<LeaseExportData> exportedLeases = await inMemoryContainer.ExportLeasesAsync("exporter");

            // Assert
            Assert.AreEqual(leaseCount, exportedLeases.Count);
        }

        [TestMethod]
        public async Task ExportLeasesAsync_RespectsCancellationToken()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> concurrentDictionary = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(concurrentDictionary);
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => inMemoryContainer.ExportLeasesAsync("exporter", cts.Token));
        }

        #endregion

        #region ImportLeasesAsync Tests

        [TestMethod]
        public async Task ImportLeasesAsync_AddsLeasesToContainer()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> concurrentDictionary = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(concurrentDictionary);

            List<LeaseExportData> leasesToImport = new List<LeaseExportData>
            {
                new LeaseExportData
                {
                    LeaseId = "imported-lease-1",
                    LeaseToken = "0",
                    LeaseVersion = nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease),
                },
                new LeaseExportData
                {
                    LeaseId = "imported-lease-2",
                    LeaseToken = "1",
                    LeaseVersion = nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease),
                }
            };

            // Act
            await inMemoryContainer.ImportLeasesAsync(leasesToImport, "importer");

            // Assert
            Assert.AreEqual(2, concurrentDictionary.Count);
            Assert.IsTrue(concurrentDictionary.ContainsKey("imported-lease-1"));
            Assert.IsTrue(concurrentDictionary.ContainsKey("imported-lease-2"));
        }

        [TestMethod]
        [DataRow(false, "original-owner", "original-token", DisplayName = "Without overwrite preserves existing")]
        [DataRow(true, "importer", "new-token", DisplayName = "With overwrite replaces existing")]
        public async Task ImportLeasesAsync_OverwriteBehavior(
            bool overwriteExisting,
            string expectedOwner,
            string expectedToken)
        {
            // Arrange
            DocumentServiceLeaseCore existingLease = new DocumentServiceLeaseCore
            {
                LeaseId = "existing-lease",
                LeaseToken = "0",
                Owner = "original-owner",
                ContinuationToken = "original-token",
            };

            ConcurrentDictionary<string, DocumentServiceLease> concurrentDictionary = new ConcurrentDictionary<string, DocumentServiceLease>();
            concurrentDictionary.TryAdd(existingLease.Id, existingLease);

            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(concurrentDictionary);

            List<LeaseExportData> leasesToImport = new List<LeaseExportData>
            {
                new LeaseExportData
                {
                    LeaseId = "existing-lease",
                    LeaseToken = "0",
                    Owner = "old-owner-from-export",
                    ContinuationToken = "new-token",
                    LeaseVersion = nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease),
                }
            };

            // Act
            await inMemoryContainer.ImportLeasesAsync(leasesToImport, "importer", overwriteExisting: overwriteExisting);

            // Assert
            Assert.AreEqual(1, concurrentDictionary.Count);
            Assert.AreEqual(expectedOwner, concurrentDictionary["existing-lease"].Owner);
            Assert.AreEqual(expectedToken, concurrentDictionary["existing-lease"].ContinuationToken);
        }

        [TestMethod]
        public async Task ImportLeasesAsync_ThrowsArgumentNullException_WhenLeasesIsNull()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> concurrentDictionary = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(concurrentDictionary);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => inMemoryContainer.ImportLeasesAsync(null, "importer"));
        }

        #endregion

        #region RoundTrip Tests

        [TestMethod]
        public async Task ExportThenImport_RoundTrip_PreservesData()
        {
            // Arrange
            DocumentServiceLeaseCore originalLease = new DocumentServiceLeaseCore
            {
                LeaseId = "roundtrip-lease",
                LeaseToken = "0",
                Owner = "original-owner",
                ContinuationToken = "original-token",
                Mode = "IncrementalFeed",
                Properties = new Dictionary<string, string> { { "custom", "value" } }
            };

            ConcurrentDictionary<string, DocumentServiceLease> sourceContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            sourceContainer.TryAdd(originalLease.Id, originalLease);
            DocumentServiceLeaseContainerInMemory source = new DocumentServiceLeaseContainerInMemory(sourceContainer);

            ConcurrentDictionary<string, DocumentServiceLease> targetContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseContainerInMemory target = new DocumentServiceLeaseContainerInMemory(targetContainer);

            // Act
            IReadOnlyList<LeaseExportData> exported = await source.ExportLeasesAsync("exporter");
            await target.ImportLeasesAsync(exported, "importer");

            // Assert
            Assert.AreEqual(1, targetContainer.Count);
            DocumentServiceLease importedLease = targetContainer["roundtrip-lease"];
            Assert.AreEqual("roundtrip-lease", importedLease.Id);
            Assert.AreEqual("0", importedLease.CurrentLeaseToken);
            Assert.AreEqual("original-token", importedLease.ContinuationToken);
            Assert.AreEqual("IncrementalFeed", importedLease.Mode);
            Assert.AreEqual("importer", importedLease.Owner); // Owner updated to importer
            Assert.IsTrue(importedLease.Properties.ContainsKey("custom"));
            Assert.AreEqual("value", importedLease.Properties["custom"]);
        }

        #endregion
    }
}