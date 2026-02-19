//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
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
            IReadOnlyList<JsonElement> exportedLeases = await inMemoryContainer.ExportLeasesAsync();

            // Assert
            Assert.AreEqual(leaseCount, exportedLeases.Count);
            foreach (JsonElement leaseElement in exportedLeases)
            {
                Assert.AreNotEqual(JsonValueKind.Undefined, leaseElement.ValueKind);
                Assert.AreNotEqual(JsonValueKind.Null, leaseElement.ValueKind);
            }
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
                () => inMemoryContainer.ExportLeasesAsync(cts.Token));
        }

        #endregion

        #region ImportLeasesAsync Tests

        [TestMethod]
        public async Task ImportLeasesAsync_ThrowsArgumentNullException_WhenLeasesIsNull()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> concurrentDictionary = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(concurrentDictionary);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => inMemoryContainer.ImportLeasesAsync(null));
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
            IReadOnlyList<JsonElement> exported = await source.ExportLeasesAsync();
            await target.ImportLeasesAsync(exported);

            // Assert
            Assert.AreEqual(1, targetContainer.Count);
            DocumentServiceLease importedLease = targetContainer["roundtrip-lease"];
            Assert.AreEqual("roundtrip-lease", importedLease.Id);
            Assert.AreEqual("0", importedLease.CurrentLeaseToken);
            Assert.AreEqual("original-token", importedLease.ContinuationToken);
            Assert.AreEqual("IncrementalFeed", importedLease.Mode);
            Assert.AreEqual("original-owner", importedLease.Owner);
        }

        [TestMethod]
        [DataRow(false, "original-owner", DisplayName = "Without overwrite preserves existing")]
        [DataRow(true, "new-owner", DisplayName = "With overwrite replaces existing")]
        public async Task ImportLeasesAsync_OverwriteBehavior(bool overwriteExisting, string expectedOwner)
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

            // Create new lease with different owner
            DocumentServiceLeaseCore newLease = new DocumentServiceLeaseCore
            {
                LeaseId = "existing-lease",
                LeaseToken = "0",
                Owner = "new-owner",
                ContinuationToken = "new-token",
            };

            // Export and create import data
            ConcurrentDictionary<string, DocumentServiceLease> tempContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            tempContainer.TryAdd(newLease.Id, newLease);
            DocumentServiceLeaseContainerInMemory tempSource = new DocumentServiceLeaseContainerInMemory(tempContainer);
            IReadOnlyList<JsonElement> leasesToImport = await tempSource.ExportLeasesAsync();

            // Act
            await inMemoryContainer.ImportLeasesAsync(leasesToImport, overwriteExisting: overwriteExisting);

            // Assert
            Assert.AreEqual(1, concurrentDictionary.Count);
            Assert.AreEqual(expectedOwner, concurrentDictionary["existing-lease"].Owner);
        }

        #endregion
    }
}