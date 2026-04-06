//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Documents.Routing;
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

        #region ShutdownAsync Tests

        [TestMethod]
        public async Task ShutdownAsync_WithNoStream_IsNoOp()
        {
            // Arrange — container without a stream
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("lease0", new DocumentServiceLeaseCore { LeaseId = "lease0", LeaseToken = "0" });
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container);

            // Act — should not throw
            await inMemoryContainer.ShutdownAsync();
        }

        [TestMethod]
        [DataRow(0, DisplayName = "Empty container persists empty array")]
        [DataRow(2, DisplayName = "Container with two leases persists both")]
        public async Task ShutdownAsync_WritesExpectedCount(int leaseCount)
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            for (int i = 0; i < leaseCount; i++)
            {
                DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk
                {
                    LeaseId = $"lease{i}",
                    LeaseToken = i.ToString(),
                    Owner = $"instance{i}",
                    FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false))
                };
                container.TryAdd(lease.Id, lease);
            }

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container);
            inMemoryContainer.LeaseStateStream = stream;

            // Act
            await inMemoryContainer.ShutdownAsync();

            // Assert
            Assert.IsTrue(stream.Length > 0 || leaseCount == 0);
            stream.Position = 0;
            string json = Encoding.UTF8.GetString(stream.ToArray());
            List<JsonElement> elements = JsonSerializer.Deserialize<List<JsonElement>>(json);
            Assert.AreEqual(leaseCount, elements.Count);
        }

        [TestMethod]
        public async Task ShutdownAsync_StreamPositionResetToZero()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("lease0", new DocumentServiceLeaseCore { LeaseId = "lease0", LeaseToken = "0" });

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container);
            inMemoryContainer.LeaseStateStream = stream;

            // Act
            await inMemoryContainer.ShutdownAsync();

            // Assert — stream position should be 0 for the next reader
            Assert.AreEqual(0, stream.Position);
        }

        #endregion

        #region RoundTrip Tests

        [TestMethod]
        public async Task PersistThenDeserialize_RoundTrip_PreservesData()
        {
            // Arrange
            DocumentServiceLeaseCoreEpk originalLease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "roundtrip-lease",
                LeaseToken = "0",
                Owner = "original-owner",
                ContinuationToken = "original-token",
                Mode = "IncrementalFeed",
                Properties = new Dictionary<string, string> { { "custom", "value" } },
                FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false))
            };

            ConcurrentDictionary<string, DocumentServiceLease> sourceContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            sourceContainer.TryAdd(originalLease.Id, originalLease);

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory source = new DocumentServiceLeaseContainerInMemory(sourceContainer);
            source.LeaseStateStream = stream;

            // Act — persist then deserialize
            await source.ShutdownAsync();

            string json = Encoding.UTF8.GetString(stream.ToArray());
            List<JsonElement> elements = JsonSerializer.Deserialize<List<JsonElement>>(json);
            Assert.AreEqual(1, elements.Count);

            DocumentServiceLease importedLease = DocumentServiceLeaseContainerInMemory.DeserializeLease(elements[0]);

            // Assert
            Assert.IsNotNull(importedLease);
            Assert.AreEqual("roundtrip-lease", importedLease.Id);
            Assert.AreEqual("0", importedLease.CurrentLeaseToken);
            Assert.AreEqual("original-token", importedLease.ContinuationToken);
            Assert.AreEqual("IncrementalFeed", importedLease.Mode);
            Assert.AreEqual("original-owner", importedLease.Owner);
        }

        [TestMethod]
        public async Task PersistOverwritesPreviousStreamContent()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("lease0", new DocumentServiceLeaseCoreEpk { LeaseId = "lease0", LeaseToken = "0", Owner = "first", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container);
            inMemoryContainer.LeaseStateStream = stream;

            // First persist
            await inMemoryContainer.ShutdownAsync();

            // Now change the lease data
            container.Clear();
            container.TryAdd("lease1", new DocumentServiceLeaseCoreEpk { LeaseId = "lease1", LeaseToken = "1", Owner = "second", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            // Second persist
            await inMemoryContainer.ShutdownAsync();

            // Assert — stream should contain only the new data
            string json = Encoding.UTF8.GetString(stream.ToArray());
            List<JsonElement> elements = JsonSerializer.Deserialize<List<JsonElement>>(json);
            Assert.AreEqual(1, elements.Count);

            DocumentServiceLease lease = DocumentServiceLeaseContainerInMemory.DeserializeLease(elements[0]);
            Assert.AreEqual("lease1", lease.Id);
            Assert.AreEqual("second", lease.Owner);
        }

        #endregion
    }
}