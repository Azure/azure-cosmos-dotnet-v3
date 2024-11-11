//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
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
    }
}