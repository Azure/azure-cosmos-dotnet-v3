//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseContainerCosmosTests
    {
        private static DocumentServiceLeaseStoreManagerSettings leaseStoreManagerSettings = new DocumentServiceLeaseStoreManagerSettings()
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

            IEnumerable<DocumentServiceLease> readLeases = await documentServiceLeaseContainerCosmos.GetAllLeasesAsync();
            CollectionAssert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases, readLeases.ToList());
        }

        [TestMethod]
        public async Task GetOwnedLeasesAsync_ReturnsOnlyMatched()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            IEnumerable<DocumentServiceLease> readLeases = await documentServiceLeaseContainerCosmos.GetOwnedLeasesAsync();
            CollectionAssert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases.Where(l => l.Owner == DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.HostName).ToList(), readLeases.ToList());
        }

        private static CosmosContainer GetMockedContainer(string containerName = "myColl")
        {
            CosmosResponseMessageHeaders headers = new CosmosResponseMessageHeaders();
            headers.Continuation = string.Empty;

            Mock<CosmosFeedIterator<DocumentServiceLeaseCore>> mockedQuery = new Mock<CosmosFeedIterator<DocumentServiceLeaseCore>>();
            mockedQuery.Setup(q => q.FetchNextSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CosmosReadFeedResponse<DocumentServiceLeaseCore>.CreateResponse(
                    responseMessageHeaders: headers,
                    resources: DocumentServiceLeaseContainerCosmosTests.allLeases,
                    hasMoreResults: false));
            mockedQuery.SetupSequence(q => q.HasMoreResults)
                .Returns(true)
                .Returns(false);

            Mock<CosmosItems> mockedItems = new Mock<CosmosItems>();
            mockedItems.Setup(i => i.CreateItemQuery<DocumentServiceLeaseCore>(
                // To make sure the SQL Query gets correctly created
                It.Is<string>(value => ("SELECT * FROM c WHERE STARTSWITH(c.id, '" + DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.GetPartitionLeasePrefix() + "')").Equals(value)), 
                It.IsAny<int>(), 
                It.IsAny<int?>(), 
                It.IsAny<string>(), 
                It.IsAny<CosmosQueryRequestOptions>()))
                .Returns(()=>
                {
                    return mockedQuery.Object;
                });

            Mock<CosmosContainer> mockedContainer = new Mock<CosmosContainer>();
            //mockedContainer.Setup(c => c.LinkUri).Returns(new Uri("/dbs/myDb/colls/" + containerName, UriKind.Relative));
            //mockedContainer.Setup(c => c.Client).Returns(DocumentServiceLeaseContainerCosmosTests.GetMockedClient());
            mockedContainer.Setup(c => c.Items).Returns(mockedItems.Object);
            return mockedContainer.Object;
        }

        private static CosmosClient GetMockedClient()
        {
            DocumentClient documentClient = new MockDocumentClient();

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());

            return cosmosClientBuilder.Build(documentClient);
        }
    }
}
