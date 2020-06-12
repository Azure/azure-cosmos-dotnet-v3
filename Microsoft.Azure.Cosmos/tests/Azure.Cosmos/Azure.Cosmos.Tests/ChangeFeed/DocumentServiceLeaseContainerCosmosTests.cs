//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseContainerCosmosTests
    {
        private static DocumentServiceLeaseStoreManagerOptions leaseStoreManagerSettings = new DocumentServiceLeaseStoreManagerOptions()
        {
            ContainerNamePrefix = "prefix",
            HostName = "host"
        };

        private static List<DocumentServiceLeaseCore> allLeases = new List<DocumentServiceLeaseCore>()
        {
            new DocumentServiceLeaseCore()
            {
                LeaseId = "1",
                Owner = "someone"
            },
            new DocumentServiceLeaseCore()
            {
                LeaseId = "2",
                Owner = "host"
            }
        };

        [TestMethod]
        public async Task GetAllLeasesAsync_ReturnsAllLeaseDocuments()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            List<DocumentServiceLease> readLeases = (await documentServiceLeaseContainerCosmos.GetAllLeasesAsync()).ToList();
            Assert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases.Count, readLeases.Count);
            Assert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases[0].Id, readLeases[0].Id);
            Assert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases[1].Id, readLeases[1].Id);
            Assert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases[0].Owner, readLeases[0].Owner);
            Assert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases[1].Owner, readLeases[1].Owner);
        }

        [TestMethod]
        public async Task GetOwnedLeasesAsync_ReturnsOnlyMatched()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            List<DocumentServiceLease> readLeases = (await documentServiceLeaseContainerCosmos.GetOwnedLeasesAsync()).ToList();
            List<DocumentServiceLeaseCore> owned = DocumentServiceLeaseContainerCosmosTests.allLeases.Where(l => l.Owner == DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.HostName).ToList();

            Assert.AreEqual(owned.Count, readLeases.Count);
            Assert.AreEqual(owned[0].Id, readLeases[0].Id);
            Assert.AreEqual(owned[0].Owner, readLeases[0].Owner);
        }

        private static CosmosContainer GetMockedContainer(string containerName = "myColl")
        {
            Headers headers = new Headers();
            headers.ContinuationToken = string.Empty;

            MockFeedResponse<DocumentServiceLeaseCore> cosmosFeedResponse = new MockFeedResponse<DocumentServiceLeaseCore>()
            {
                Documents = DocumentServiceLeaseContainerCosmosTests.allLeases
            };

            ResponseMessage mockFeedResponse = new ResponseMessage()
            {
                Content = CosmosTextJsonSerializer.CreateSerializer().ToStream(cosmosFeedResponse)
            };

            Mock<IAsyncEnumerable<Response>> mockEnumerable = new Mock<IAsyncEnumerable<Response>>();
            mockEnumerable.Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(new MockAsyncEnumerator<Response>(new List<Response>() { mockFeedResponse }));

            Mock<CosmosContainer> mockedItems = new Mock<CosmosContainer>();
            mockedItems.Setup(i => i.GetItemQueryStreamResultsAsync(
                // To make sure the SQL Query gets correctly created
                It.Is<string>(value => string.Equals("SELECT * FROM c WHERE STARTSWITH(c.id, '" + DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.GetPartitionLeasePrefix() + "')", value)),
                It.IsAny<string>(), 
                It.IsAny<QueryRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(()=>
                {
                    return mockEnumerable.Object;
                });

            return mockedItems.Object;
        }

        private class MockAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly List<T> results;
            private int currentIndex = 0;
            public MockAsyncEnumerator(List<T> results)
            {
                this.results = results;
            }
            public T Current => this.results[this.currentIndex++];

            public ValueTask DisposeAsync()
            {
                return new ValueTask(Task.CompletedTask);
            }

            public ValueTask<bool> MoveNextAsync()
            {
                if (this.currentIndex == this.results.Count)
                {
                    return new ValueTask<bool>(Task.FromResult(false));
                }

                return new ValueTask<bool>(Task.FromResult(true));
            }
        }


        private class MockFeedResponse<T>
        {
            public List<T> Documents { get; set; }
        }
    }
}
