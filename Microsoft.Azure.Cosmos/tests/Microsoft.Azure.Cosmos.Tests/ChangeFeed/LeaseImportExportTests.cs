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
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class LeaseImportExportTests
    {
        private static readonly DocumentServiceLeaseStoreManagerOptions leaseStoreManagerSettings = new DocumentServiceLeaseStoreManagerOptions()
        {
            ContainerNamePrefix = "prefix",
            HostName = "host"
        };

        [TestMethod]
        public async Task ExportLeasesAsync_WithEpkLeases_Succeeds()
        {
            // Arrange
            List<DocumentServiceLease> leasesWithMetadata = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseId = "lease1",
                    LeaseToken = "100",
                    Owner = "host1",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
                },
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseId = "lease2",
                    LeaseToken = "200",
                    Owner = "host2",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("FF", "FFFF", true, false))
                },
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "containerId.info",  // Metadata document (exported without FeedRange validation)
                    LeaseToken = null,
                    FeedRange = null
                }
            };

            Container mockContainer = GetMockedContainerForExport(leasesWithMetadata);
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer,
                leaseStoreManagerSettings);

            // Act
            IReadOnlyList<JsonElement> exportedLeases = await leaseContainer.ExportLeasesAsync();

            // Assert
            Assert.AreEqual(3, exportedLeases.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ExportLeasesAsync_WithNonEpkLeases_ThrowsException()
        {
            // Arrange - Leases without FeedRange (non-EPK)
            List<DocumentServiceLeaseCore> nonEpkLeases = new List<DocumentServiceLeaseCore>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "lease1",
                    LeaseToken = "0",
                    Owner = "host1",
                    FeedRange = null  // No FeedRange = not EPK-based
                }
            };

            Container mockContainer = GetMockedContainerForExport(nonEpkLeases.Cast<DocumentServiceLease>().ToList());
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer,
                leaseStoreManagerSettings);

            // Act - should throw
            await leaseContainer.ExportLeasesAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ExportLeasesAsync_WithMixedLeaseTypes_ThrowsException()
        {
            // Arrange - Mix of EPK and non-EPK leases
            List<DocumentServiceLease> mixedLeases = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseId = "lease1",
                    LeaseToken = "100",
                    Owner = "host1",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
                },
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "lease2",
                    LeaseToken = "1",
                    Owner = "host2",
                    FeedRange = null  // No FeedRange = not EPK-based
                }
            };

            Container mockContainer = GetMockedContainerForExport(mixedLeases);
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer,
                leaseStoreManagerSettings);

            // Act - should throw when it encounters the non-EPK lease
            await leaseContainer.ExportLeasesAsync();
        }

        [TestMethod]
        public async Task ExportLeasesAsync_WithLeaseHavingFeedRange_Succeeds()
        {
            // Arrange - Legacy version 0 leases that have been migrated to EPK (have FeedRange)
            List<DocumentServiceLeaseCore> legacyEpkLeases = new List<DocumentServiceLeaseCore>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "lease1",
                    LeaseToken = "1",
                    Owner = "host1",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
                },
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "lease2",
                    LeaseToken = "2",
                    Owner = "host2",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("FF", "FFFF", true, false))
                }
            };

            Container mockContainer = GetMockedContainerForExport(legacyEpkLeases.Cast<DocumentServiceLease>().ToList());
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer,
                leaseStoreManagerSettings);

            // Act
            IReadOnlyList<JsonElement> exportedLeases = await leaseContainer.ExportLeasesAsync();

            // Assert
            Assert.AreEqual(legacyEpkLeases.Count, exportedLeases.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ImportLeasesAsync_WithNonEpkLeases_ThrowsException()
        {
            // Arrange - Lease without FeedRange (non-EPK)
            DocumentServiceLeaseCore nonEpkLease = new DocumentServiceLeaseCore()
            {
                LeaseId = "lease1",
                LeaseToken = "0",
                Owner = "host1",
                FeedRange = null  // No FeedRange = not EPK-based
            };

            string leaseJson = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(nonEpkLease).ReadAsString();
            List<JsonElement> leasesToImport = new List<JsonElement>
            {
                JsonDocument.Parse(leaseJson).RootElement
            };

            Mock<Container> mockContainer = new Mock<Container>();
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer.Object,
                leaseStoreManagerSettings);

            // Act - should throw
            await leaseContainer.ImportLeasesAsync(leasesToImport, overwriteExisting: false);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ImportLeasesAsync_WithMixedLeaseTypes_ThrowsException()
        {
            // Arrange - Mix of EPK and non-EPK leases
            DocumentServiceLeaseCoreEpk epkLease = new DocumentServiceLeaseCoreEpk()
            {
                LeaseId = "lease1",
                LeaseToken = "100",
                Owner = "host1",
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
            };

            DocumentServiceLeaseCore nonEpkLease = new DocumentServiceLeaseCore()
            {
                LeaseId = "lease2",
                LeaseToken = "1",
                Owner = "host2",
                FeedRange = null  // No FeedRange = not EPK-based
            };

            string epkLeaseJson = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(epkLease).ReadAsString();
            string nonEpkLeaseJson = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(nonEpkLease).ReadAsString();
            
            List<JsonElement> leasesToImport = new List<JsonElement>
            {
                JsonDocument.Parse(epkLeaseJson).RootElement,
                JsonDocument.Parse(nonEpkLeaseJson).RootElement
            };

            Mock<Container> mockContainer = new Mock<Container>();
            mockContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<Microsoft.Azure.Cosmos.PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.Created));

            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer.Object,
                leaseStoreManagerSettings);

            // Act - should throw when it encounters the non-EPK lease
            await leaseContainer.ImportLeasesAsync(leasesToImport, overwriteExisting: false);
        }

        [TestMethod]
        public async Task ImportLeasesAsync_WithOverwriteExisting_CallsUpsert()
        {
            // Arrange
            DocumentServiceLeaseCoreEpk epkLease = new DocumentServiceLeaseCoreEpk()
            {
                LeaseId = "lease1",
                LeaseToken = "100",
                Owner = "host1",
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false)),
                ContinuationToken = "token123"
            };

            string leaseJson = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(epkLease).ReadAsString();
            List<JsonElement> leasesToImport = new List<JsonElement>
            {
                JsonDocument.Parse(leaseJson).RootElement
            };

            ResponseMessage successResponse = new ResponseMessage(System.Net.HttpStatusCode.OK);
            Mock<Container> mockContainer = new Mock<Container>();
            mockContainer.Setup(c => c.UpsertItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<Microsoft.Azure.Cosmos.PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResponse);

            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer.Object,
                leaseStoreManagerSettings);

            // Act
            await leaseContainer.ImportLeasesAsync(leasesToImport, overwriteExisting: true);

            // Assert - verify upsert was called
            mockContainer.Verify(c => c.UpsertItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<Microsoft.Azure.Cosmos.PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        private static Container GetMockedContainerForExport(List<DocumentServiceLease> leases)
        {
            Headers headers = new Headers
            {
                ContinuationToken = string.Empty
            };

            MockFeedResponse<DocumentServiceLease> cosmosFeedResponse = new MockFeedResponse<DocumentServiceLease>()
            {
                Documents = leases
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

            Mock<Container> mockedContainer = new Mock<Container>();
            mockedContainer.Setup(c => c.GetItemQueryStreamIterator(
                It.Is<string>(value => string.Equals("SELECT * FROM c WHERE STARTSWITH(c.id, '" + leaseStoreManagerSettings.ContainerNamePrefix + "')", value)),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
                .Returns(() => mockedQuery.Object);

            return mockedContainer.Object;
        }

        private class MockFeedResponse<T>
        {
            public List<T> Documents { get; set; }
        }
    }

    internal static class StreamExtensions
    {
        public static string ReadAsString(this Stream stream)
        {
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
