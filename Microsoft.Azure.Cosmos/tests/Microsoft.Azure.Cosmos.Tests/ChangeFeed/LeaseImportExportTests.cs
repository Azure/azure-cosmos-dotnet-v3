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
            List<DocumentServiceLeaseCoreEpk> epkLeases = new List<DocumentServiceLeaseCoreEpk>()
            {
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseId = "lease1",
                    LeaseToken = "0-100",
                    Owner = "host1",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
                },
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseId = "lease2",
                    LeaseToken = "100-200",
                    Owner = "host2",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("FF", "FFFF", true, false))
                }
            };

            Container mockContainer = GetMockedContainerForExport(epkLeases.Cast<DocumentServiceLease>().ToList());
            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer,
                leaseStoreManagerSettings);

            // Act
            IReadOnlyList<JsonElement> exportedLeases = await leaseContainer.ExportLeasesAsync();

            // Assert
            Assert.AreEqual(epkLeases.Count, exportedLeases.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ExportLeasesAsync_WithNonEpkLeases_ThrowsException()
        {
            // Arrange
            List<DocumentServiceLeaseCore> nonEpkLeases = new List<DocumentServiceLeaseCore>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "lease1",
                    LeaseToken = "0",
                    Owner = "host1"
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
                    LeaseToken = "0-100",
                    Owner = "host1",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
                },
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "lease2",
                    LeaseToken = "1",
                    Owner = "host2"
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
        public async Task ImportLeasesAsync_WithEpkLeases_Succeeds()
        {
            // Arrange
            DocumentServiceLeaseCoreEpk epkLease = new DocumentServiceLeaseCoreEpk()
            {
                LeaseId = "lease1",
                LeaseToken = "0-100",
                Owner = "host1",
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false)),
                ContinuationToken = "token123"
            };

            string leaseJson = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(epkLease).ReadAsString();
            List<JsonElement> leasesToImport = new List<JsonElement>
            {
                JsonDocument.Parse(leaseJson).RootElement
            };

            bool createCalled = false;
            Mock<Container> mockContainer = new Mock<Container>();
            mockContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<Microsoft.Azure.Cosmos.PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback(() => createCalled = true)
                .ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.Created));

            DocumentServiceLeaseContainerCosmos leaseContainer = new DocumentServiceLeaseContainerCosmos(
                mockContainer.Object,
                leaseStoreManagerSettings);

            // Act
            await leaseContainer.ImportLeasesAsync(leasesToImport, overwriteExisting: false);

            // Assert
            Assert.IsTrue(createCalled);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseOperationNotSupportedException))]
        public async Task ImportLeasesAsync_WithNonEpkLeases_ThrowsException()
        {
            // Arrange
            DocumentServiceLeaseCore nonEpkLease = new DocumentServiceLeaseCore()
            {
                LeaseId = "lease1",
                LeaseToken = "0",
                Owner = "host1"
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
                LeaseToken = "0-100",
                Owner = "host1",
                FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false))
            };

            DocumentServiceLeaseCore nonEpkLease = new DocumentServiceLeaseCore()
            {
                LeaseId = "lease2",
                LeaseToken = "1",
                Owner = "host2"
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
                LeaseToken = "0-100",
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

        [TestMethod]
        public async Task ExportImportRoundTrip_WithEpkLeases_PreservesLeaseData()
        {
            // Arrange
            List<DocumentServiceLeaseCoreEpk> originalLeases = new List<DocumentServiceLeaseCoreEpk>()
            {
                new DocumentServiceLeaseCoreEpk()
                {
                    LeaseId = "lease1",
                    LeaseToken = "0-100",
                    Owner = "host1",
                    FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false)),
                    ContinuationToken = "continuation1"
                }
            };

            Container exportMockContainer = GetMockedContainerForExport(originalLeases.Cast<DocumentServiceLease>().ToList());
            DocumentServiceLeaseContainerCosmos exportLeaseContainer = new DocumentServiceLeaseContainerCosmos(
                exportMockContainer,
                leaseStoreManagerSettings);

            // Export
            IReadOnlyList<JsonElement> exportedLeases = await exportLeaseContainer.ExportLeasesAsync();

            // Setup import mock
            DocumentServiceLease importedLease = null;
            Mock<Container> importMockContainer = new Mock<Container>();
            importMockContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<Microsoft.Azure.Cosmos.PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<Stream, Microsoft.Azure.Cosmos.PartitionKey, ItemRequestOptions, CancellationToken>((stream, pk, options, ct) =>
                {
                    stream.Position = 0;
                    importedLease = CosmosContainerExtensions.DefaultJsonSerializer.FromStream<DocumentServiceLease>(stream);
                })
                .ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.Created));

            DocumentServiceLeaseContainerCosmos importLeaseContainer = new DocumentServiceLeaseContainerCosmos(
                importMockContainer.Object,
                leaseStoreManagerSettings);

            // Import
            await importLeaseContainer.ImportLeasesAsync(exportedLeases, overwriteExisting: false);

            // Assert - verify data was preserved
            Assert.IsNotNull(importedLease);
            Assert.IsInstanceOfType(importedLease, typeof(DocumentServiceLeaseCoreEpk));
            DocumentServiceLeaseCoreEpk importedEpkLease = importedLease as DocumentServiceLeaseCoreEpk;
            Assert.AreEqual(originalLeases[0].LeaseId, importedEpkLease.LeaseId);
            Assert.AreEqual(originalLeases[0].LeaseToken, importedEpkLease.LeaseToken);
            Assert.AreEqual(originalLeases[0].Owner, importedEpkLease.Owner);
            Assert.AreEqual(originalLeases[0].ContinuationToken, importedEpkLease.ContinuationToken);
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
                It.Is<string>(value => string.Equals("SELECT * FROM c WHERE STARTSWITH(c.id, '" + leaseStoreManagerSettings.GetPartitionLeasePrefix() + "')", value)),
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
