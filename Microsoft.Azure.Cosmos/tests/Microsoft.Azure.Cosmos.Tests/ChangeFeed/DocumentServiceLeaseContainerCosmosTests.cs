//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseContainerCosmosTests
    {
        private static readonly DocumentServiceLeaseStoreManagerOptions leaseStoreManagerSettings = new DocumentServiceLeaseStoreManagerOptions()
        {
            ContainerNamePrefix = "prefix",
            HostName = "host"
        };

        private static readonly List<DocumentServiceLeaseCore> allLeases = new List<DocumentServiceLeaseCore>()
        {
            new DocumentServiceLeaseCore()
            {
                LeaseId = "1",
                LeaseToken = "2",
                Owner = "someone"
            },
            new DocumentServiceLeaseCore()
            {
                LeaseId = "2",
                LeaseToken = "2",
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

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ExportLeasesAsync_WithNonEpkLeases_ThrowsUnsupportedException()
        {
            // Arrange - The existing test uses DocumentServiceLeaseCore which is non-EPK
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            // Act & Assert - Should throw LeaseOperationNotSupportedException
            await documentServiceLeaseContainerCosmos.ExportLeasesAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ImportLeasesAsync_WithNonEpkLeases_ThrowsUnsupportedException()
        {
            // Arrange
            DocumentServiceLeaseCore nonEpkLease = new DocumentServiceLeaseCore()
            {
                LeaseId = "lease1",
                LeaseToken = "0",
                Owner = "host1"
            };

            string leaseJson;
            using (Stream stream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(nonEpkLease))
            using (StreamReader reader = new StreamReader(stream))
            {
                leaseJson = reader.ReadToEnd();
            }
            
            List<JsonElement> leasesToImport = new List<JsonElement>
            {
                JsonDocument.Parse(leaseJson).RootElement
            };

            Mock<Container> mockContainer = new Mock<Container>();
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer.Object,
                leaseStoreManagerSettings);

            // Act & Assert - Should throw LeaseOperationNotSupportedException
            await leaseContainer.ImportLeasesAsync(leasesToImport, overwriteExisting: false);
        }

        private static Container GetMockedContainer()
        {
            Headers headers = new Headers
            {
                ContinuationToken = string.Empty
            };

            MockFeedResponse<DocumentServiceLeaseCore> cosmosFeedResponse = new MockFeedResponse<DocumentServiceLeaseCore>()
            {
                Documents = DocumentServiceLeaseContainerCosmosTests.allLeases
            };

            ResponseMessage mockFeedResponse = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(cosmosFeedResponse)
            };

            Mock<FeedIterator> mockedQuery = new Mock<FeedIterator>();
            mockedQuery.Setup(q => q.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => mockFeedResponse);
            mockedQuery.SetupSequence(q => q.HasMoreResults)
                .Returns(true)
                .Returns(false);

            Mock<Container> mockedItems = new Mock<Container>();
            mockedItems.Setup(i => i.GetItemQueryStreamIterator(
                // To make sure the SQL Query gets correctly created
                It.Is<string>(value => string.Equals("SELECT * FROM c WHERE STARTSWITH(c.id, '" + DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.GetPartitionLeasePrefix() + "')", value)),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
                .Returns(() => mockedQuery.Object);

            // Export uses ContainerNamePrefix (broader query to include metadata documents)
            mockedItems.Setup(i => i.GetItemQueryStreamIterator(
                It.Is<string>(value => string.Equals("SELECT * FROM c WHERE STARTSWITH(c.id, '" + DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.ContainerNamePrefix + "')", value)),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
                .Returns(() => mockedQuery.Object);

            return mockedItems.Object;
        }

        private class MockFeedResponse<T>
        {
            public List<T> Documents { get; set; }
        }
    }
}