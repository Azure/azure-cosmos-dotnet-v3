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
    }
}
