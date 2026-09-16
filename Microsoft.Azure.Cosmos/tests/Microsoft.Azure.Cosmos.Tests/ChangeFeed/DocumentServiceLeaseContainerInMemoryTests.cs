//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

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
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, stream);

            // Act
            await inMemoryContainer.ShutdownAsync();

            // Assert
            Assert.IsTrue(stream.Length > 0, "Stream should contain data even for an empty lease list (serialized as []).");
            stream.Position = 0;
            List<DocumentServiceLease> deserialized = DeserializeLeasesFromStream(stream);
            Assert.AreEqual(leaseCount, deserialized.Count);
        }

        [TestMethod]
        public async Task ShutdownAsync_StreamPositionResetToZero()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("lease0", new DocumentServiceLeaseCoreEpk { LeaseId = "lease0", LeaseToken = "0", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, stream);

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
            DateTime originalTimestamp = new DateTime(2023, 6, 15, 12, 34, 56, DateTimeKind.Utc);
            DocumentServiceLeaseCoreEpk originalLease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "roundtrip-lease",
                LeaseToken = "0",
                Owner = "original-owner",
                ContinuationToken = "original-token",
                Mode = "IncrementalFeed",
                Properties = new Dictionary<string, string>
                {
                    { "custom", "value" },
                    { "unicode", "日本語" },
                },
                FeedRange = new FeedRangeEpk(new Range<string>("AA", "BB", true, false)),
                Timestamp = originalTimestamp,
            };

            ConcurrentDictionary<string, DocumentServiceLease> sourceContainer = new ConcurrentDictionary<string, DocumentServiceLease>();
            sourceContainer.TryAdd(originalLease.Id, originalLease);

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory source = new DocumentServiceLeaseContainerInMemory(sourceContainer, stream);

            // Act — persist then deserialize through the StoreManager so we exercise the
            // same code path that customers hit via WithInMemoryLeaseContainer(stream).
            await source.ShutdownAsync();

            DocumentServiceLeaseStoreManagerInMemory restoredManager = new DocumentServiceLeaseStoreManagerInMemory(stream);
            IReadOnlyList<DocumentServiceLease> restored = await restoredManager.LeaseContainer.GetAllLeasesAsync();
            Assert.AreEqual(1, restored.Count);

            DocumentServiceLease importedLease = restored[0];

            // Assert — scalar fields are preserved verbatim.
            Assert.IsNotNull(importedLease);
            Assert.AreEqual("roundtrip-lease", importedLease.Id);
            Assert.AreEqual("0", importedLease.CurrentLeaseToken);
            Assert.AreEqual("original-token", importedLease.ContinuationToken);
            Assert.AreEqual("IncrementalFeed", importedLease.Mode);
            Assert.AreEqual("original-owner", importedLease.Owner);

            // Properties (including non-ASCII values) round-trip.
            Assert.IsNotNull(importedLease.Properties);
            Assert.AreEqual(2, importedLease.Properties.Count);
            Assert.AreEqual("value", importedLease.Properties["custom"]);
            Assert.AreEqual("日本語", importedLease.Properties["unicode"]);

            // FeedRange shape and values round-trip.
            Assert.IsInstanceOfType(importedLease.FeedRange, typeof(FeedRangeEpk));
            FeedRangeEpk importedFeedRange = (FeedRangeEpk)importedLease.FeedRange;
            Assert.AreEqual("AA", importedFeedRange.Range.Min);
            Assert.AreEqual("BB", importedFeedRange.Range.Max);
            Assert.IsTrue(importedFeedRange.Range.IsMinInclusive);
            Assert.IsFalse(importedFeedRange.Range.IsMaxInclusive);

            // Timestamp is preserved verbatim (confirms H3 no-mutation behavior).
            Assert.AreEqual(originalTimestamp, importedLease.Timestamp.ToUniversalTime());

            // After restore, the stream is rewound to 0 so callers can re-read it.
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task PersistOverwritesPreviousStreamContent()
        {
            // Arrange
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("lease0", new DocumentServiceLeaseCoreEpk { LeaseId = "lease0", LeaseToken = "0", Owner = "first", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, stream);

            // First persist
            await inMemoryContainer.ShutdownAsync();

            // Now change the lease data
            container.Clear();
            container.TryAdd("lease1", new DocumentServiceLeaseCoreEpk { LeaseId = "lease1", LeaseToken = "1", Owner = "second", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            // Second persist
            await inMemoryContainer.ShutdownAsync();

            // Assert — stream should contain only the new data
            stream.Position = 0;
            List<DocumentServiceLease> deserialized = DeserializeLeasesFromStream(stream);
            Assert.AreEqual(1, deserialized.Count);
            Assert.AreEqual("lease1", deserialized[0].Id);
            Assert.AreEqual("second", deserialized[0].Owner);
        }

        #endregion

        private static List<DocumentServiceLease> DeserializeLeasesFromStream(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream, leaveOpen: true))
            using (JsonTextReader jsonReader = new JsonTextReader(sr))
            {
                return JsonSerializer.Create().Deserialize<List<DocumentServiceLease>>(jsonReader);
            }
        }

        #region Deserialize Tests

        [TestMethod]
        public void Deserialize_DuplicateIds_Throws()
        {
            // Arrange — hand-crafted JSON with two leases sharing the same id.
            string duplicateJson =
                "[" +
                "{\"id\":\"dup\",\"Owner\":\"o1\",\"LeaseToken\":\"0\",\"ContinuationToken\":\"c1\"}," +
                "{\"id\":\"dup\",\"Owner\":\"o2\",\"LeaseToken\":\"1\",\"ContinuationToken\":\"c2\"}" +
                "]";

            MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(duplicateJson));

            // Act & Assert — restore should fail fast, not silently overwrite.
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => new DocumentServiceLeaseStoreManagerInMemory(stream));

            Assert.IsTrue(ex.Message.Contains("duplicate lease id"), $"Unexpected message: {ex.Message}");
            Assert.IsTrue(ex.Message.Contains("dup"), $"Unexpected message: {ex.Message}");
        }

        [TestMethod]
        public async Task Deserialize_LeavesStreamPositionAtZero()
        {
            // Arrange — serialize some leases first.
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("l0", new DocumentServiceLeaseCoreEpk { LeaseId = "l0", LeaseToken = "0", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            MemoryStream stream = new MemoryStream();
            await new DocumentServiceLeaseContainerInMemory(container, stream).ShutdownAsync();

            // Seek to end to simulate a stream reused across multiple operations.
            stream.Position = stream.Length;

            // Act — deserialization should rewind the stream.
            _ = new DocumentServiceLeaseStoreManagerInMemory(stream);

            // Assert — stream is at position 0, ready to be re-read by the caller.
            Assert.AreEqual(0, stream.Position);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public async Task ShutdownAsync_WithNonEpkLease_StillSerializes()
        {
            // Arrange — non-EPK lease (no FeedRange)
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("core-lease", new DocumentServiceLeaseCore { LeaseId = "core-lease", LeaseToken = "0", Owner = "owner" });

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, stream);

            // Act
            await inMemoryContainer.ShutdownAsync();

            // Assert — lease is serialized
            Assert.IsTrue(stream.Length > 0);
            stream.Position = 0;
            List<DocumentServiceLease> deserialized = DeserializeLeasesFromStream(stream);
            Assert.AreEqual(1, deserialized.Count);
            Assert.AreEqual("core-lease", deserialized[0].Id);
        }

        [TestMethod]
        public async Task ShutdownAsync_WithDisposedStream_Throws()
        {
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd("lease0", new DocumentServiceLeaseCoreEpk { LeaseId = "lease0", LeaseToken = "0", FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false)) });

            MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, stream);

            stream.Dispose();

            // Disposed MemoryStream reports CanWrite=false, so SetLength throws NotSupportedException
            // which the container surfaces as a descriptive InvalidOperationException (same shape
            // as the non-resizable-stream-too-small path).
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => inMemoryContainer.ShutdownAsync());
        }

        [TestMethod]
        public async Task ShutdownAsync_WithNonResizableStream_SameSizeData_WritesSuccessfully()
        {
            // Arrange — simulate the documented restore pattern: new MemoryStream(byte[])
            // which creates a non-expandable stream.
            DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = "0",
                LeaseToken = "0",
                ContinuationToken = "1",
                Owner = "owner",
                FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false))
            };

            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd(lease.Id, lease);

            // First, serialize to get realistic bytes (non-expandable stream needs sufficient capacity)
            MemoryStream temp = new MemoryStream();
            DocumentServiceLeaseContainerInMemory seeder = new DocumentServiceLeaseContainerInMemory(container, temp);
            await seeder.ShutdownAsync();

            // Create a non-expandable stream from the byte array (matches user pattern: new MemoryStream(File.ReadAllBytes(...)))
            byte[] savedState = temp.ToArray();
            MemoryStream nonResizable = new MemoryStream(savedState);

            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, nonResizable);

            // Act — should not throw NotSupportedException
            await inMemoryContainer.ShutdownAsync();

            // Assert
            Assert.AreEqual(0, nonResizable.Position);
            Assert.IsTrue(nonResizable.Length > 0);
            List<DocumentServiceLease> deserialized = DeserializeLeasesFromStream(nonResizable);
            Assert.AreEqual(1, deserialized.Count);
            Assert.AreEqual("0", deserialized[0].Id);
            Assert.AreEqual("1", deserialized[0].ContinuationToken);
        }

        [TestMethod]
        public async Task ShutdownAsync_WithNonResizableStream_LargerData_ThrowsInvalidOperation()
        {
            // Arrange — start with a small non-expandable stream, then add more leases
            // so serialized output exceeds the original buffer capacity.
            byte[] smallBuffer = System.Text.Encoding.UTF8.GetBytes("[]");
            MemoryStream nonResizable = new MemoryStream(smallBuffer);

            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            for (int i = 0; i < 10; i++)
            {
                DocumentServiceLeaseCoreEpk lease = new DocumentServiceLeaseCoreEpk
                {
                    LeaseId = i.ToString(),
                    LeaseToken = i.ToString(),
                    ContinuationToken = $"continuation-{i}",
                    Owner = "owner",
                    FeedRange = new FeedRangeEpk(new Range<string>("", "FF", true, false))
                };
                container.TryAdd(lease.Id, lease);
            }

            DocumentServiceLeaseContainerInMemory inMemoryContainer = new DocumentServiceLeaseContainerInMemory(container, nonResizable);

            // Act & Assert — should throw InvalidOperationException with helpful message
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => inMemoryContainer.ShutdownAsync());

            Assert.IsTrue(ex.Message.Contains("not expandable"));
            Assert.IsInstanceOfType(ex.InnerException, typeof(NotSupportedException));
        }

        #endregion
    }
}