//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseManagerCosmosTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ChangeFeedLeaseIdAsPartitionKeyEnabled, null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ChangeFeedLeaseIdAsPartitionKeyEnabled, null);
        }

        /// <summary>
        /// Verifies that the update lambda updates the owner.
        /// </summary>
        [TestMethod]
        public async Task AcquireCompletes()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                DocumentServiceLease afterUpdateLease = updater(lease);
                return options.HostName == afterUpdateLease.Owner;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.AcquireAsync(lease);

            Assert.AreEqual(options.HostName, afterAcquire.Owner);
        }

        [DataTestMethod]
        [DataRow(0, DisplayName = "Container with system PK")]
        [DataRow(1, DisplayName = "Container with id PK")]
        [DataRow(2, DisplayName = "Container with partitionKey PK")]
        public async Task CreatesEPKBasedLease(int factoryType)
        {
            RequestOptionsFactory requestOptionsFactory = GetRequestOptionsFactory(factoryType);
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            FeedRangeEpk feedRangeEpk = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions options, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                requestOptionsFactory);

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(feedRangeEpk, continuation);

            DocumentServiceLeaseCoreEpk epkBasedLease = (DocumentServiceLeaseCoreEpk)afterAcquire;

            Assert.IsNotNull(epkBasedLease);
            Assert.AreEqual(continuation, afterAcquire.ContinuationToken);
            Assert.AreEqual(feedRangeEpk.Range.Min, ((FeedRangeEpk)epkBasedLease.FeedRange).Range.Min);
            Assert.AreEqual(feedRangeEpk.Range.Max, ((FeedRangeEpk)epkBasedLease.FeedRange).Range.Max);
            ValidateRequestOptionsFactory(requestOptionsFactory, epkBasedLease);
            if (requestOptionsFactory is PartitionedByPartitionKeyCollectionRequestOptionsFactory)
            {
                Assert.AreEqual(epkBasedLease.Id, epkBasedLease.PartitionKey);
            }
        }

        [DataTestMethod]
        [DataRow(0, DisplayName = "Container with system PK")]
        [DataRow(1, DisplayName = "Container with id PK")]
        [DataRow(2, DisplayName = "Container with partitionKey PK")]
        public async Task CreatesPartitionKeyBasedLease(int factoryType)
        {
            RequestOptionsFactory requestOptionsFactory = GetRequestOptionsFactory(factoryType);
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions options, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                requestOptionsFactory);

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(partitionKeyRange, continuation);

            DocumentServiceLeaseCore pkRangeBasedLease = (DocumentServiceLeaseCore)afterAcquire;

            Assert.IsNotNull(pkRangeBasedLease);
            Assert.AreEqual(continuation, afterAcquire.ContinuationToken);
            Assert.AreEqual(partitionKeyRange.Id, pkRangeBasedLease.CurrentLeaseToken);
            ValidateRequestOptionsFactory(requestOptionsFactory, pkRangeBasedLease);
            if (requestOptionsFactory is PartitionedByPartitionKeyCollectionRequestOptionsFactory)
            {
                Assert.AreEqual(pkRangeBasedLease.Id, pkRangeBasedLease.PartitionKey);
            }
        }

        [TestMethod]
        public async Task CreatesPartitionKeyBasedLeaseWithDeterministicPartitionKeyByDefault()
        {
            PartitionKey? capturedPartitionKey = null;
            RequestOptionsFactory requestOptionsFactory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).Callback((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => capturedPartitionKey = partitionKey)
                .ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                Mock.Of<DocumentServiceLeaseUpdater>(),
                options,
                requestOptionsFactory);

            DocumentServiceLease lease = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(partitionKeyRange, continuation);

            Assert.IsTrue(capturedPartitionKey.HasValue);
            Assert.AreEqual(new PartitionKey(lease.Id), capturedPartitionKey.Value);
            Assert.AreEqual(lease.Id, lease.PartitionKey);
        }

        [TestMethod]
        public async Task CreatesEPKBasedLeaseWithLegacyGuidPartitionKeyWhenOptedOut()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ChangeFeedLeaseIdAsPartitionKeyEnabled, "false");

            PartitionKey? capturedPartitionKey = null;
            RequestOptionsFactory requestOptionsFactory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            FeedRangeEpk feedRangeEpk = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).Callback((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => capturedPartitionKey = partitionKey)
                .ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                Mock.Of<DocumentServiceLeaseUpdater>(),
                options,
                requestOptionsFactory);

            DocumentServiceLease lease = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(feedRangeEpk, continuation);

            Assert.IsTrue(capturedPartitionKey.HasValue);
            Assert.AreNotEqual(new PartitionKey(lease.Id), capturedPartitionKey.Value);
            Assert.IsTrue(Guid.TryParse(lease.PartitionKey, out _));
        }

        [TestMethod]
        public async Task CreatesEPKBasedLeaseWithDeterministicPartitionKeyByDefault()
        {
            PartitionKey? capturedPartitionKey = null;
            RequestOptionsFactory requestOptionsFactory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            FeedRangeEpk feedRangeEpk = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).Callback((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => capturedPartitionKey = partitionKey)
                .ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                Mock.Of<DocumentServiceLeaseUpdater>(),
                options,
                requestOptionsFactory);

            DocumentServiceLease lease = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(feedRangeEpk, continuation);

            Assert.IsTrue(capturedPartitionKey.HasValue);
            Assert.AreEqual(new PartitionKey(lease.Id), capturedPartitionKey.Value);
            Assert.AreEqual(lease.Id, lease.PartitionKey);
        }

        [TestMethod]
        public async Task CreatesPartitionKeyBasedLeaseWithLegacyGuidPartitionKeyWhenOptedOut()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ChangeFeedLeaseIdAsPartitionKeyEnabled, "false");

            PartitionKey? capturedPartitionKey = null;
            RequestOptionsFactory requestOptionsFactory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).Callback((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => capturedPartitionKey = partitionKey)
                .ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken token) => new ResponseMessage(System.Net.HttpStatusCode.OK) { Content = stream });

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                Mock.Of<DocumentServiceLeaseUpdater>(),
                options,
                requestOptionsFactory);

            DocumentServiceLease lease = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(partitionKeyRange, continuation);

            Assert.IsTrue(capturedPartitionKey.HasValue);
            Assert.AreNotEqual(new PartitionKey(lease.Id), capturedPartitionKey.Value);
            Assert.IsTrue(Guid.TryParse(lease.PartitionKey, out _));
        }

        /// <summary>
        /// Verifies that when CreateItemStreamAsync returns 409 Conflict (another host created the lease first),
        /// the dedup chain returns null, proving the deterministic PK ensures conflict detection works.
        /// Tests both PKRange and EPK overloads.
        /// </summary>
        [TestMethod]
        public async Task CreateLeaseIfNotExistAsync_Returns409Conflict_ReturnsNull_PKRange()
        {
            RequestOptionsFactory requestOptionsFactory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange()
            {
                Id = "0",
                MinInclusive = "",
                MaxExclusive = "FF"
            };

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.Conflict));

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                Mock.Of<DocumentServiceLeaseUpdater>(),
                options,
                requestOptionsFactory);

            DocumentServiceLease result = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(partitionKeyRange, continuation);

            Assert.IsNull(result, "When 409 Conflict is returned, CreateLeaseIfNotExistAsync should return null (dedup).");
        }

        [TestMethod]
        public async Task CreateLeaseIfNotExistAsync_Returns409Conflict_ReturnsNull_EPK()
        {
            RequestOptionsFactory requestOptionsFactory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            string continuation = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            FeedRangeEpk feedRangeEpk = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.Conflict));

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                Mock.Of<DocumentServiceLeaseUpdater>(),
                options,
                requestOptionsFactory);

            DocumentServiceLease result = await documentServiceLeaseManagerCosmos.CreateLeaseIfNotExistAsync(feedRangeEpk, continuation);

            Assert.IsNull(result, "When 409 Conflict is returned, CreateLeaseIfNotExistAsync should return null (dedup).");
        }

        /// <summary>
        /// Verifies back-compatibility: a pre-existing lease with a random GUID partition key
        /// (created before the deterministic PK fix) can still be read, acquired, renewed, and released
        /// because all downstream operations use the stored lease.PartitionKey value.
        /// </summary>
        [TestMethod]
        public async Task AcquireCompletes_WithPreExistingGuidPartitionKey()
        {
            string guidPartitionKey = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseId = "some-prefix..0",
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                LeasePartitionKey = guidPartitionKey,
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.Is<string>(id => id == lease.LeaseId),
                It.Is<PartitionKey>(pk => pk == new PartitionKey(guidPartitionKey)),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                new PartitionedByPartitionKeyCollectionRequestOptionsFactory());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.AcquireAsync(lease);

            Assert.IsNotNull(afterAcquire);
            Assert.AreEqual(guidPartitionKey, afterAcquire.PartitionKey, "Old GUID partition key must be preserved through acquire.");
        }

        [TestMethod]
        public async Task RenewCompletes_WithPreExistingGuidPartitionKey()
        {
            string guidPartitionKey = Guid.NewGuid().ToString();
            string hostName = Guid.NewGuid().ToString();
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = hostName
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseId = "some-prefix..0",
                LeaseToken = "0",
                Owner = hostName,
                LeasePartitionKey = guidPartitionKey,
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            ResponseMessage leaseResponse = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lease)
            };

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.ReadItemStreamAsync(
                It.Is<string>(id => id == lease.LeaseId),
                It.Is<PartitionKey>(pk => pk == new PartitionKey(guidPartitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();
            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.Is<string>(id => id == lease.LeaseId),
                It.Is<PartitionKey>(pk => pk == new PartitionKey(guidPartitionKey)),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                new PartitionedByPartitionKeyCollectionRequestOptionsFactory());

            DocumentServiceLease afterRenew = await documentServiceLeaseManagerCosmos.RenewAsync(lease);

            Assert.IsNotNull(afterRenew);
            Assert.AreEqual(guidPartitionKey, afterRenew.PartitionKey, "Old GUID partition key must be preserved through renew.");
        }

        /// <summary>
        /// Verifies a Release sets the owner on null
        /// </summary>
        [TestMethod]
        public async Task ReleaseCompletes()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseId = Guid.NewGuid().ToString(),
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                DocumentServiceLease afterUpdateLease = updater(lease);
                return afterUpdateLease.Owner == null;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lease)
            };

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.ReadItemStreamAsync(
                It.Is<string>(id => id == lease.LeaseId),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);


            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            await documentServiceLeaseManagerCosmos.ReleaseAsync(lease);
        }

        /// <summary>
        /// Verifies a Release when the lease has already been deleted does not throw
        /// </summary>
        [TestMethod]
        public async Task ReleaseWhenNotExistDoesNotThrow()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseId = Guid.NewGuid().ToString(),
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.NotFound);

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.ReadItemStreamAsync(
                It.Is<string>(id => id == lease.LeaseId),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);


            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            await documentServiceLeaseManagerCosmos.ReleaseAsync(lease);

            mockUpdater.Verify(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()), Times.Never);
        }

        /// <summary>
        /// Verifies a Release when attempting to access the lease returns a 404 with some particular substatus
        /// </summary>
        [TestMethod]
        public async Task ReleaseWhen404WithSomeSubstatusDoesThrow()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseId = Guid.NewGuid().ToString(),
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.NotFound);
            leaseResponse.Headers.SubStatusCode = Documents.SubStatusCodes.ReadSessionNotAvailable;

            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.ReadItemStreamAsync(
                It.Is<string>(id => id == lease.LeaseId),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);


            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                mockedContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => documentServiceLeaseManagerCosmos.ReleaseAsync(lease));

            Assert.AreEqual(System.Net.HttpStatusCode.NotFound, cosmosException.StatusCode);
            Assert.AreEqual((int)Documents.SubStatusCodes.ReadSessionNotAvailable, cosmosException.SubStatusCode);

            mockUpdater.Verify(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()), Times.Never);
        }

        /// <summary>
        /// Verifies that if the updater read a different Owner from the captured in memory, throws a LeaseLost
        /// </summary>
        [TestMethod]
        public async Task IfOwnerChangedThrow()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                lease.Owner = Guid.NewGuid().ToString();
                DocumentServiceLease afterUpdateLease = updater(lease);
                return true;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => documentServiceLeaseManagerCosmos.AcquireAsync(lease));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.PreconditionFailed);
        }

        /// <summary>
        /// Verifies that if the renewed read a different Owner from the captured in memory, throws a LeaseLost
        /// </summary>
        [TestMethod]
        public async Task IfOwnerChangedThrowOnRenew()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                DocumentServiceLeaseCore serverLease = new DocumentServiceLeaseCore()
                {
                    LeaseToken = "0",
                    Owner = Guid.NewGuid().ToString(),
                    FeedRange = new FeedRangePartitionKeyRange("0")
                };
                DocumentServiceLease afterUpdateLease = updater(serverLease);
                return true;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lease)
            };

            Mock<ContainerInternal> leaseContainer = new Mock<ContainerInternal>();
            leaseContainer.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => documentServiceLeaseManagerCosmos.RenewAsync(lease));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.PreconditionFailed);
        }

        /// <summary>
        /// Verifies that if the update properties read a different Owner from the captured in memory, throws a LeaseLost
        /// </summary>
        [TestMethod]
        public async Task IfOwnerChangedThrowOnUpdateProperties()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = lease.Owner
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                DocumentServiceLeaseCore serverLease = new DocumentServiceLeaseCore()
                {
                    LeaseToken = "0",
                    Owner = Guid.NewGuid().ToString(),
                    FeedRange = new FeedRangePartitionKeyRange("0")
                };
                DocumentServiceLease afterUpdateLease = updater(serverLease);
                return true;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lease)
            };

            Mock<ContainerInternal> leaseContainer = new Mock<ContainerInternal>();
            leaseContainer.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => documentServiceLeaseManagerCosmos.UpdatePropertiesAsync(lease));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.PreconditionFailed);
        }

        /// <summary>
        /// Verifies that if the update properties read a different Owner from the captured in memory, throws a LeaseLost
        /// </summary>
        [TestMethod]
        public async Task IfOwnerChangedThrowOnRelease()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = new FeedRangePartitionKeyRange("0")
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                DocumentServiceLeaseCore serverLease = new DocumentServiceLeaseCore()
                {
                    LeaseToken = "0",
                    Owner = Guid.NewGuid().ToString(),
                    FeedRange = new FeedRangePartitionKeyRange("0")
                };
                DocumentServiceLease afterUpdateLease = updater(serverLease);
                return true;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            ResponseMessage leaseResponse = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(lease)
            };

            Mock<ContainerInternal> leaseContainer = new Mock<ContainerInternal>();
            leaseContainer.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(leaseResponse);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                leaseContainer.Object,
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => documentServiceLeaseManagerCosmos.ReleaseAsync(lease));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.PreconditionFailed);
        }

        /// <summary>
        /// When a lease is missing the range information, check that we are adding it
        /// </summary>
        [TestMethod]
        public async Task PopulateMissingRange()
        {
            DocumentServiceLeaseStoreManagerOptions options = new DocumentServiceLeaseStoreManagerOptions
            {
                HostName = Guid.NewGuid().ToString()
            };

            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString()
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
                // Simulate dirty read from db
                DocumentServiceLease afterUpdateLease = updater(lease);
                return afterUpdateLease.FeedRange != null;
            };

            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.Is<Func<DocumentServiceLease, DocumentServiceLease>>(f => validateUpdater(f))))
                .ReturnsAsync(lease);

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhot");
            containerMock.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync("test");
            MockDocumentClient mockDocumentClient = new MockDocumentClient();
            mockContext.Setup(c => c.DocumentClient).Returns(mockDocumentClient);

            DocumentServiceLeaseManagerCosmos documentServiceLeaseManagerCosmos = new DocumentServiceLeaseManagerCosmos(
                containerMock.Object,
                Mock.Of<ContainerInternal>(),
                mockUpdater.Object,
                options,
                Mock.Of<RequestOptionsFactory>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerCosmos.AcquireAsync(lease);

            Assert.IsNotNull(afterAcquire.FeedRange);
        }

        private static RequestOptionsFactory GetRequestOptionsFactory(int factoryType)
        {
            return factoryType switch
            {
                0 => new SinglePartitionRequestOptionsFactory(),
                1 => new PartitionedByIdCollectionRequestOptionsFactory(),
                2 => new PartitionedByPartitionKeyCollectionRequestOptionsFactory(),
                _ => throw new Exception($"Unkown value for FactoryType: {factoryType}."),
            };
        }

        private static void ValidateRequestOptionsFactory(RequestOptionsFactory requestOptionsFactory, DocumentServiceLease lease)
        {
            if (requestOptionsFactory is SinglePartitionRequestOptionsFactory)
            {
                Assert.IsNull(lease.PartitionKey);
            }
            else if (requestOptionsFactory is PartitionedByIdCollectionRequestOptionsFactory)
            {
                Assert.IsNull(lease.PartitionKey);
            }
            else if (requestOptionsFactory is PartitionedByPartitionKeyCollectionRequestOptionsFactory)
            {
                Assert.IsNotNull(lease.PartitionKey);
            }
            else
            {
                throw new Exception($"Unkown type mapping for FactoryType: {requestOptionsFactory.GetType()}.");
            }
        }
    }
}
