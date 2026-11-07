//------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseManagerInMemoryTests
    {
        [DataTestMethod]
        [DataRow(false, null)]
        [DataRow(true, null)]
        [DataRow(false, "fingerprint")]
        [DataRow(true, "fingerprint")]
        public async Task NewLeasesUseConfiguredQueryIdentity(bool epk, string hash)
        {
            ConcurrentDictionary<string, DocumentServiceLease> state = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseManager manager = CreateManager(state, hash);

            DocumentServiceLease lease = await CreateLeaseAsync(manager, epk);

            Assert.AreEqual(hash, lease.QueryFilterHash);
            Assert.AreEqual("progress", lease.ContinuationToken);
            Assert.AreSame(lease, state[lease.Id]);
        }

        [DataTestMethod]
        [DataRow(false, null, null, true)]
        [DataRow(true, null, null, true)]
        [DataRow(false, "fingerprint", "fingerprint", true)]
        [DataRow(true, "fingerprint", "fingerprint", true)]
        [DataRow(false, "fingerprint", null, false)]
        [DataRow(true, "fingerprint", null, false)]
        [DataRow(false, null, "fingerprint", false)]
        [DataRow(true, null, "fingerprint", false)]
        [DataRow(false, "fingerprint", "other", false)]
        [DataRow(true, "fingerprint", "other", false)]
        [DataRow(false, null, "", false)]
        [DataRow(true, null, "", false)]
        public async Task CreateConflictChecksStoredQueryIdentity(bool epk, string configuredHash, string storedHash, bool compatible)
        {
            ConcurrentDictionary<string, DocumentServiceLease> state = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLease existing = await CreateLeaseAsync(CreateManager(state, storedHash), epk);
            DocumentServiceLeaseManager contender = CreateManager(state, configuredHash);

            if (compatible)
            {
                Assert.IsNull(await CreateLeaseAsync(contender, epk));
            }
            else
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => CreateLeaseAsync(contender, epk));
            }

            Assert.AreSame(existing, state[existing.Id]);
            Assert.AreEqual(storedHash, existing.QueryFilterHash);
            Assert.AreEqual("progress", existing.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow("Acquire")]
        [DataRow("Release")]
        [DataRow("Renew")]
        [DataRow("Properties")]
        public async Task CachedQueryMismatchIsRejectedBeforeUpdate(string operation)
        {
            ConcurrentDictionary<string, DocumentServiceLease> state = new ConcurrentDictionary<string, DocumentServiceLease>();
            Mock<DocumentServiceLeaseUpdater> updater = new Mock<DocumentServiceLeaseUpdater>(MockBehavior.Strict);
            DocumentServiceLeaseManager manager = new DocumentServiceLeaseManagerInMemory(updater.Object, state);
            manager.ConfigureQueryFilter("configured");
            DocumentServiceLease lease = new DocumentServiceLeaseCore { QueryFilterHash = "other" };

            await AssertQueryMismatchAsync(manager, operation, lease);

            updater.VerifyNoOtherCalls();
            Assert.IsNull(lease.FeedRange);
        }

        [DataTestMethod]
        [DataRow("Acquire")]
        [DataRow("Release")]
        [DataRow("Renew")]
        [DataRow("Properties")]
        public async Task OperationsRejectChangedStoredIdentity(string operation)
        {
            ConcurrentDictionary<string, DocumentServiceLease> state = new ConcurrentDictionary<string, DocumentServiceLease>();
            DocumentServiceLeaseManager manager = CreateManager(state, "fingerprint");
            DocumentServiceLease stored = await CreateLeaseAsync(manager, epk: false);
            stored.Owner = "owner";
            DocumentServiceLease cached = JsonConvert.DeserializeObject<DocumentServiceLease>(JsonConvert.SerializeObject(stored));
            stored.QueryFilterHash = "other";

            await AssertQueryMismatchAsync(manager, operation, cached);

            Assert.AreSame(stored, state[stored.Id]);
            Assert.AreEqual("other", stored.QueryFilterHash);
            Assert.AreEqual("owner", stored.Owner);
            Assert.AreEqual("progress", stored.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow(false, null, null, true)]
        [DataRow(true, null, null, true)]
        [DataRow(false, "fingerprint", "fingerprint", true)]
        [DataRow(true, "fingerprint", "fingerprint", true)]
        [DataRow(false, "fingerprint", null, false)]
        [DataRow(true, "fingerprint", null, false)]
        [DataRow(false, null, "fingerprint", false)]
        [DataRow(true, null, "fingerprint", false)]
        [DataRow(false, "fingerprint", "other", false)]
        [DataRow(true, "fingerprint", "other", false)]
        public async Task RestoredLeasesRetainIdentityAcrossOperations(bool epk, string storedHash, string configuredHash, bool compatible)
        {
            using MemoryStream stream = new MemoryStream();
            DocumentServiceLeaseStoreManagerInMemory original = new DocumentServiceLeaseStoreManagerInMemory(
                new ConcurrentDictionary<string, DocumentServiceLease>(), stream);
            original.LeaseManager.ConfigureQueryFilter(storedHash);
            DocumentServiceLease source = await CreateLeaseAsync(original.LeaseManager, epk);
            source.Owner = "owner";
            await original.ShutdownAsync();
            DocumentServiceLeaseStoreManagerInMemory restored = new DocumentServiceLeaseStoreManagerInMemory(stream);
            restored.LeaseManager.ConfigureQueryFilter(configuredHash);
            DocumentServiceLease lease = (await restored.LeaseContainer.GetAllLeasesAsync()).Single();
            Assert.AreEqual(source.GetType(), lease.GetType());
            Assert.AreEqual(storedHash, lease.QueryFilterHash);

            if (!compatible)
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => restored.LeaseManager.AcquireAsync(lease));
                Assert.AreEqual("owner", lease.Owner);
                Assert.AreEqual("progress", lease.ContinuationToken);
                Assert.AreEqual(storedHash, lease.QueryFilterHash);
                return;
            }

            lease = await restored.LeaseManager.AcquireAsync(lease);
            lease.Properties["custom"] = "value";
            lease = await restored.LeaseManager.UpdatePropertiesAsync(lease);
            lease = await restored.LeaseCheckpointer.CheckpointAsync(lease, "next-progress");
            lease = await restored.LeaseManager.RenewAsync(lease);
            await restored.LeaseManager.ReleaseAsync(lease);

            Assert.AreEqual(storedHash, lease.QueryFilterHash);
            Assert.AreEqual("next-progress", lease.ContinuationToken);
            Assert.AreEqual("value", lease.Properties["custom"]);
            Assert.IsNull(lease.Owner);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task FreshAcquisitionPreservesRangeBackfill(bool storedRangeExists)
        {
            FeedRangeInternal cachedRange = new FeedRangePartitionKeyRange("cached");
            FeedRangeInternal storedRange = storedRangeExists ? new FeedRangePartitionKeyRange("stored") : null;
            DocumentServiceLease cached = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", QueryFilterHash = "fingerprint", FeedRange = cachedRange
            };
            DocumentServiceLease stored = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", QueryFilterHash = "fingerprint", FeedRange = storedRange
            };
            ConcurrentDictionary<string, DocumentServiceLease> state = new ConcurrentDictionary<string, DocumentServiceLease>();
            state.TryAdd(stored.Id, stored);
            DocumentServiceLeaseManager manager = CreateManager(state, "fingerprint");

            DocumentServiceLease acquired = await manager.AcquireAsync(cached);

            Assert.AreSame(stored, acquired);
            Assert.AreSame(storedRange ?? cachedRange, acquired.FeedRange);
        }

        private static DocumentServiceLeaseManager CreateManager(ConcurrentDictionary<string, DocumentServiceLease> state, string hash)
        {
            DocumentServiceLeaseManager manager = new DocumentServiceLeaseManagerInMemory(new DocumentServiceLeaseUpdaterInMemory(state), state);
            manager.ConfigureQueryFilter(hash);
            return manager;
        }

        private static Task<DocumentServiceLease> CreateLeaseAsync(DocumentServiceLeaseManager manager, bool epk)
        {
            return epk
                ? manager.CreateLeaseIfNotExistAsync(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)), "progress")
                : manager.CreateLeaseIfNotExistAsync(new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" }, "progress");
        }

        private static async Task AssertQueryMismatchAsync(DocumentServiceLeaseManager manager, string operation, DocumentServiceLease lease)
        {
            if (operation == "Renew")
            {
                LeaseLostException exception = await Assert.ThrowsExceptionAsync<LeaseLostException>(
                    () => RunLeaseOperationAsync(manager, operation, lease));
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                Assert.IsFalse(exception.IsGone);
            }
            else
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => RunLeaseOperationAsync(manager, operation, lease));
            }
        }

        private static Task RunLeaseOperationAsync(DocumentServiceLeaseManager manager, string operation, DocumentServiceLease lease)
        {
            return operation switch
            {
                "Acquire" => manager.AcquireAsync(lease),
                "Release" => manager.ReleaseAsync(lease),
                "Renew" => manager.RenewAsync(lease),
                "Properties" => manager.UpdatePropertiesAsync(lease),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
        }
    }
}
