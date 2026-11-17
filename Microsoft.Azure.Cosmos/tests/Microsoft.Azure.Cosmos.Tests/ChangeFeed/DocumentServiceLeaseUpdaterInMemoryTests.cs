//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseUpdaterInMemoryTests
    {
        delegate bool Updates(out DocumentServiceLease lease);

        [TestMethod]
        [ExpectedException(typeof(LeaseLostException))]
        public async Task RetriesIfCannotFind()
        {
            string itemId = "1";
            string partitionKey = "1";

            List<KeyValuePair<string, DocumentServiceLease>> state = new List<KeyValuePair<string, DocumentServiceLease>>();

            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>(state);

            DocumentServiceLeaseUpdaterInMemory updater = new DocumentServiceLeaseUpdaterInMemory(container);
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, new Cosmos.PartitionKey(partitionKey), serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });
        }

        [TestMethod]
        public async Task UpdatesLease()
        {
            string itemId = "1";
            string partitionKey = "1";

            List<KeyValuePair<string, DocumentServiceLease>> state = new List<KeyValuePair<string, DocumentServiceLease>>()
            {
                new KeyValuePair<string, DocumentServiceLease>( itemId, new DocumentServiceLeaseCore() )
            };

            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>(state);

            DocumentServiceLeaseUpdaterInMemory updater = new DocumentServiceLeaseUpdaterInMemory(container);
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, new Cosmos.PartitionKey(partitionKey), serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Assert.AreEqual("newHost", updatedLease.Owner);
        }

        [TestMethod]
        public async Task UpdatesUseLatestStoredLease()
        {
            DocumentServiceLease cached = new DocumentServiceLeaseCore
            {
                LeaseId = "1",
                LeaseToken = "1",
                QueryFilterHash = "cached",
                ContinuationToken = "old-progress"
            };
            DocumentServiceLease current = new DocumentServiceLeaseCore
            {
                LeaseId = "1",
                LeaseToken = "1",
                QueryFilterHash = "persisted",
                ContinuationToken = "current-progress"
            };
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd(current.Id, current);
            DocumentServiceLeaseUpdaterInMemory updater = new DocumentServiceLeaseUpdaterInMemory(container);

            DocumentServiceLease updated = await updater.UpdateLeaseAsync(cached, cached.Id, PartitionKey.Null, lease => lease);

            Assert.AreSame(current, updated);
            Assert.AreEqual("persisted", container[current.Id].QueryFilterHash);
            Assert.AreEqual("current-progress", container[current.Id].ContinuationToken);
        }

        [TestMethod]
        public async Task RetriesUseLatestStoredLease()
        {
            DocumentServiceLease original = new DocumentServiceLeaseCore { LeaseId = "1", LeaseToken = "1", QueryFilterHash = "original" };
            DocumentServiceLease replacement = new DocumentServiceLeaseCore { LeaseId = "1", LeaseToken = "1", QueryFilterHash = "replacement" };
            ConcurrentDictionary<string, DocumentServiceLease> container = new ConcurrentDictionary<string, DocumentServiceLease>();
            container.TryAdd(original.Id, original);
            DocumentServiceLeaseUpdaterInMemory updater = new DocumentServiceLeaseUpdaterInMemory(container);
            List<DocumentServiceLease> observed = new List<DocumentServiceLease>();

            DocumentServiceLease updated = await updater.UpdateLeaseAsync(original, original.Id, PartitionKey.Null, lease =>
            {
                observed.Add(lease);
                if (observed.Count == 1)
                {
                    container[original.Id] = replacement;
                }

                return lease;
            });

            CollectionAssert.AreEqual(new[] { original, replacement }, observed);
            Assert.AreSame(replacement, updated);
            Assert.AreSame(replacement, container[original.Id]);
        }
    }
}