//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    [DoNotParallelize]
    public class DocumentServiceLeaseManagerCosmosTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            DocumentServiceLeaseManagerCosmos.IsChangeFeedLeaseIdAsPartitionKeyEnabled = ConfigurationManager.IsChangeFeedLeaseIdAsPartitionKeyEnabled();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DocumentServiceLeaseManagerCosmos.IsChangeFeedLeaseIdAsPartitionKeyEnabled = ConfigurationManager.IsChangeFeedLeaseIdAsPartitionKeyEnabled();
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
        }

        /// <summary>
        /// Verifies partition key behavior for both overloads and both flag states:
        /// default (deterministic PK = lease id) and opt-out (legacy GUID PK).
        /// </summary>
        [DataTestMethod]
        [DataRow("PKRange", true, true, DisplayName = "PKRange: deterministic PK by default")]
        [DataRow("PKRange", false, false, DisplayName = "PKRange: legacy GUID PK when opted out")]
        [DataRow("EPK", true, true, DisplayName = "EPK: deterministic PK by default")]
        [DataRow("EPK", false, false, DisplayName = "EPK: legacy GUID PK when opted out")]
        public async Task CreateLeaseIfNotExistAsync_PartitionKeyBehavior(string overloadType, bool flagValue, bool expectDeterministic)
        {
            DocumentServiceLeaseManagerCosmos.IsChangeFeedLeaseIdAsPartitionKeyEnabled = flagValue;

            PartitionKey? capturedPartitionKey = null;
            Mock<ContainerInternal> mockedContainer = CreatePkCapturingMockContainer(pk => capturedPartitionKey = pk);
            DocumentServiceLeaseManagerCosmos manager = CreateLeaseManager(mockedContainer.Object, new PartitionedByPartitionKeyCollectionRequestOptionsFactory());

            DocumentServiceLease lease = await CreateLeaseAsync(manager, overloadType, Guid.NewGuid().ToString());

            Assert.IsTrue(capturedPartitionKey.HasValue);
            if (expectDeterministic)
            {
                Assert.AreEqual(new PartitionKey(lease.Id), capturedPartitionKey.Value);
                Assert.AreEqual(lease.Id, lease.PartitionKey);
            }
            else
            {
                Assert.AreNotEqual(new PartitionKey(lease.Id), capturedPartitionKey.Value);
                Assert.IsTrue(Guid.TryParse(lease.PartitionKey, out _));
            }
        }

        /// <summary>
        /// Verifies the 409 dedup chain by simulating Cosmos DB's uniqueness constraint:
        /// a duplicate (PartitionKey, id) combination returns 409 Conflict regardless of call order.
        /// This proves the deterministic PK value ensures both creates land in the same logical
        /// partition and trigger the id-uniqueness conflict.
        /// </summary>
        [DataTestMethod]
        [DataRow("PKRange", DisplayName = "PKRange: dedup via (PK, id) uniqueness")]
        [DataRow("EPK", DisplayName = "EPK: dedup via (PK, id) uniqueness")]
        public async Task CreateLeaseIfNotExistAsync_DuplicatePkId_Returns409(string overloadType)
        {
            Mock<ContainerInternal> mockedContainer = CreateDedupSimulatingMockContainer();
            DocumentServiceLeaseManagerCosmos manager = CreateLeaseManager(mockedContainer.Object, new PartitionedByPartitionKeyCollectionRequestOptionsFactory());
            string continuation = Guid.NewGuid().ToString();

            DocumentServiceLease firstResult = await CreateLeaseAsync(manager, overloadType, continuation);
            Assert.IsNotNull(firstResult, "First create should succeed with 200.");

            DocumentServiceLease secondResult = await CreateLeaseAsync(manager, overloadType, continuation);
            Assert.IsNull(secondResult, "Second create should return null due to 409 Conflict (same PK + id dedup).");
        }

        /// <summary>
        /// Verifies back-compatibility: a pre-existing lease with a random GUID partition key
        /// (created before the deterministic PK fix) can still be acquired because
        /// downstream operations use the stored lease.PartitionKey value.
        /// </summary>
        [TestMethod]
        public async Task AcquireCompletes_WithPreExistingGuidPartitionKey()
        {
            DocumentServiceLeaseCore lease = CreatePreExistingGuidPkLease(out string guidPartitionKey, out DocumentServiceLeaseStoreManagerOptions options);

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

        /// <summary>
        /// Verifies back-compatibility: a pre-existing lease with a random GUID partition key
        /// (created before the deterministic PK fix) can still be renewed because
        /// downstream operations use the stored lease.PartitionKey value.
        /// </summary>
        [TestMethod]
        public async Task RenewCompletes_WithPreExistingGuidPartitionKey()
        {
            DocumentServiceLeaseCore lease = CreatePreExistingGuidPkLease(out string guidPartitionKey, out DocumentServiceLeaseStoreManagerOptions options);

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
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task IfOwnerChangedThrowOnRenew(bool changedQuery)
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
                    QueryFilterHash = changedQuery ? "other-query" : null,
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

            if (changedQuery)
            {
                Assert.IsInstanceOfType(leaseLost.InnerException, typeof(ArgumentException));
            }
            else
            {
                Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                    && innerCosmosException.StatusCode == HttpStatusCode.PreconditionFailed);
            }
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

        /// <summary>
        /// Mirrors <see cref="PopulateMissingRange"/> for <see cref="DocumentServiceLeaseManagerInMemory"/>:
        /// a lease missing its range (e.g. restored from previously persisted in-memory lease state) should
        /// have it resolved and backfilled on acquire.
        /// </summary>
        [TestMethod]
        public async Task InMemoryManager_PopulateMissingRange()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString()
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();

            Func<Func<DocumentServiceLease, DocumentServiceLease>, bool> validateUpdater = (Func<DocumentServiceLease, DocumentServiceLease> updater) =>
            {
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
            containerMock.Setup(c => c.LinkUri).Returns("http://localhost");
            containerMock.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync("test");
            MockDocumentClient mockDocumentClient = new MockDocumentClient();
            mockContext.Setup(c => c.DocumentClient).Returns(mockDocumentClient);

            DocumentServiceLeaseManagerInMemory documentServiceLeaseManagerInMemory = new DocumentServiceLeaseManagerInMemory(
                mockUpdater.Object,
                new ConcurrentDictionary<string, DocumentServiceLease>(),
                containerMock.Object);

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerInMemory.AcquireAsync(lease);

            Assert.IsNotNull(afterAcquire.FeedRange);
        }

        /// <summary>
        /// When <see cref="DocumentServiceLeaseManagerInMemory"/> is constructed without a monitored
        /// container (e.g. via the original constructor overload used before this fix, or any caller that
        /// doesn't have one available), acquiring a lease with a missing FeedRange must remain a no-op for
        /// that field instead of throwing - preserving pre-existing behavior for that code path.
        /// </summary>
        [TestMethod]
        public async Task InMemoryManager_NoMonitoredContainer_DoesNotPopulateOrThrow()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString()
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();
            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .ReturnsAsync(lease);

            DocumentServiceLeaseManagerInMemory documentServiceLeaseManagerInMemory = new DocumentServiceLeaseManagerInMemory(
                mockUpdater.Object,
                new ConcurrentDictionary<string, DocumentServiceLease>());

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerInMemory.AcquireAsync(lease);

            Assert.IsNull(afterAcquire.FeedRange);
        }

        /// <summary>
        /// A lease that already has a FeedRange must not be touched or re-resolved on acquire - the
        /// backfill only applies to leases missing a FeedRange.
        /// </summary>
        [TestMethod]
        public async Task InMemoryManager_ExistingFeedRange_IsNotOverwritten()
        {
            FeedRangeEpk existingFeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("", "FF", true, false));
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString(),
                FeedRange = existingFeedRange
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();
            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .ReturnsAsync(lease);

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();

            DocumentServiceLeaseManagerInMemory documentServiceLeaseManagerInMemory = new DocumentServiceLeaseManagerInMemory(
                mockUpdater.Object,
                new ConcurrentDictionary<string, DocumentServiceLease>(),
                containerMock.Object);

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerInMemory.AcquireAsync(lease);

            Assert.AreSame(existingFeedRange, afterAcquire.FeedRange);
            // No routing-cache/RID calls should be made when there is nothing to backfill.
            containerMock.Verify(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// If the lease's PartitionKeyRangeId can no longer be resolved (e.g. the range is already gone),
        /// the backfill must leave FeedRange null rather than throwing, matching
        /// <see cref="DocumentServiceLeaseManagerCosmos.AcquireAsync"/>'s behavior for the same case.
        /// </summary>
        [TestMethod]
        public async Task InMemoryManager_UnresolvableRange_LeavesFeedRangeNull()
        {
            DocumentServiceLeaseCore lease = new DocumentServiceLeaseCore()
            {
                LeaseToken = "0",
                Owner = Guid.NewGuid().ToString()
            };

            Mock<DocumentServiceLeaseUpdater> mockUpdater = new Mock<DocumentServiceLeaseUpdater>();
            mockUpdater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .ReturnsAsync(lease);

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhost");
            containerMock.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync("test");
            UnresolvableRangeMockDocumentClient mockDocumentClient = new UnresolvableRangeMockDocumentClient();
            mockContext.Setup(c => c.DocumentClient).Returns(mockDocumentClient);

            DocumentServiceLeaseManagerInMemory documentServiceLeaseManagerInMemory = new DocumentServiceLeaseManagerInMemory(
                mockUpdater.Object,
                new ConcurrentDictionary<string, DocumentServiceLease>(),
                containerMock.Object);

            DocumentServiceLease afterAcquire = await documentServiceLeaseManagerInMemory.AcquireAsync(lease);

            Assert.IsNull(afterAcquire.FeedRange);
        }

        /// <summary>
        /// <see cref="MockDocumentClient"/> variant that always fails to resolve a PartitionKeyRange by id,
        /// simulating a lease whose original range is no longer present in the routing map cache.
        /// </summary>
        private class UnresolvableRangeMockDocumentClient : MockDocumentClient
        {
            internal override Documents.PartitionKeyRange ResolvePartitionKeyRangeById(string collectionRid, string pkRangeId, bool forceRefresh)
            {
                return null;
            }
        }

        [DataTestMethod]
        [DataRow("PKRange", null)]
        [DataRow("EPK", null)]
        [DataRow("PKRange", "fingerprint")]
        [DataRow("EPK", "fingerprint")]
        public async Task NewLeasesUseConfiguredQueryIdentity(string kind, string hash)
        {
            Mock<ContainerInternal> container = CreateDedupSimulatingMockContainer();
            DocumentServiceLeaseManagerCosmos manager = CreateLeaseManager(
                container.Object, new PartitionedByIdCollectionRequestOptionsFactory());
            manager.ConfigureQueryFilter(hash);

            DocumentServiceLease lease = await CreateLeaseAsync(manager, kind, "progress");

            Assert.AreEqual(hash, lease.QueryFilterHash);
            Assert.AreEqual("progress", lease.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow("PKRange", null, null, true)]
        [DataRow("EPK", null, null, true)]
        [DataRow("PKRange", "fingerprint", "fingerprint", true)]
        [DataRow("EPK", "fingerprint", "fingerprint", true)]
        [DataRow("PKRange", "fingerprint", null, false)]
        [DataRow("EPK", "fingerprint", null, false)]
        [DataRow("PKRange", null, "fingerprint", false)]
        [DataRow("EPK", null, "fingerprint", false)]
        [DataRow("PKRange", "fingerprint", "other", false)]
        [DataRow("EPK", "fingerprint", "other", false)]
        [DataRow("PKRange", null, "", false)]
        [DataRow("EPK", null, "", false)]
        public async Task CreateConflictChecksStoredQueryIdentity(string kind, string configuredHash, string storedHash, bool compatible)
        {
            Mock<ContainerInternal> container = CreateDedupSimulatingMockContainer();
            DocumentServiceLeaseManagerCosmos original = CreateLeaseManager(
                container.Object, new PartitionedByIdCollectionRequestOptionsFactory());
            original.ConfigureQueryFilter(storedHash);
            DocumentServiceLease existing = await CreateLeaseAsync(original, kind, "original-progress");
            DocumentServiceLeaseManagerCosmos contender = CreateLeaseManager(
                container.Object, new PartitionedByIdCollectionRequestOptionsFactory());
            contender.ConfigureQueryFilter(configuredHash);

            if (compatible)
            {
                Assert.IsNull(await CreateLeaseAsync(contender, kind, "replacement-progress"));
            }
            else
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => CreateLeaseAsync(contender, kind, "replacement-progress"));
            }

            Assert.AreEqual(storedHash, existing.QueryFilterHash);
            Assert.AreEqual("original-progress", existing.ContinuationToken);
            container.Verify(c => c.ReadItemStreamAsync(
                existing.Id, It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [DataTestMethod]
        [DataRow("Acquire")]
        [DataRow("Release")]
        [DataRow("Renew")]
        [DataRow("Properties")]
        public async Task CachedQueryMismatchIsRejectedBeforeStoreAccess(string operation)
        {
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            Mock<DocumentServiceLeaseUpdater> updater = new Mock<DocumentServiceLeaseUpdater>(MockBehavior.Strict);
            DocumentServiceLeaseManagerCosmos manager = CreateLeaseManager(
                container.Object, new PartitionedByIdCollectionRequestOptionsFactory(), updater.Object);
            manager.ConfigureQueryFilter("configured");
            DocumentServiceLease lease = new DocumentServiceLeaseCore { QueryFilterHash = "other" };

            await AssertQueryMismatchAsync(manager, operation, lease);

            container.VerifyNoOtherCalls();
            updater.VerifyNoOtherCalls();
            Assert.IsNull(lease.FeedRange);
        }

        [DataTestMethod]
        [DataRow("Acquire", null, null, true)]
        [DataRow("Release", null, null, true)]
        [DataRow("Renew", null, null, true)]
        [DataRow("Properties", null, null, true)]
        [DataRow("Acquire", "fingerprint", "fingerprint", true)]
        [DataRow("Release", "fingerprint", "fingerprint", true)]
        [DataRow("Renew", "fingerprint", "fingerprint", true)]
        [DataRow("Properties", "fingerprint", "fingerprint", true)]
        [DataRow("Acquire", "fingerprint", "other", false)]
        [DataRow("Release", "fingerprint", "other", false)]
        [DataRow("Renew", "fingerprint", "other", false)]
        [DataRow("Properties", "fingerprint", "other", false)]
        public async Task UpdateCallbacksCheckIdentityAndPreserveProgress(string operation, string configuredHash, string storedHash, bool compatible)
        {
            DocumentServiceLease lease = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", Owner = "host", QueryFilterHash = configuredHash,
                FeedRange = new FeedRangePartitionKeyRange("0")
            };
            DocumentServiceLease server = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", Owner = "host", QueryFilterHash = storedHash,
                ContinuationToken = "server-progress"
            };
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK) { Content = new CosmosJsonDotNetSerializer().ToStream(lease) });
            Mock<DocumentServiceLeaseUpdater> updater = new Mock<DocumentServiceLeaseUpdater>();
            updater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(), It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .Returns((DocumentServiceLease cached, string id, PartitionKey pk, Func<DocumentServiceLease, DocumentServiceLease> update) =>
                    Task.FromResult(update(server)));
            DocumentServiceLeaseManagerCosmos manager = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(), container.Object, updater.Object,
                new DocumentServiceLeaseStoreManagerOptions { HostName = "host" }, new PartitionedByIdCollectionRequestOptionsFactory());
            manager.ConfigureQueryFilter(configuredHash);

            if (compatible)
            {
                await RunLeaseOperationAsync(manager, operation, lease);
            }
            else
            {
                await AssertQueryMismatchAsync(manager, operation, lease);
            }

            Assert.AreEqual(storedHash, server.QueryFilterHash);
            Assert.AreEqual("server-progress", server.ContinuationToken);
            Assert.AreEqual(compatible && operation == "Release" ? null : "host", server.Owner);
        }

        [DataTestMethod]
        [DataRow("Acquire")]
        [DataRow("Release")]
        [DataRow("Renew")]
        [DataRow("Properties")]
        public async Task EtagRetryRejectsNewlyIncompatibleLease(string operation)
        {
            DocumentServiceLease lease = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", Owner = "host", QueryFilterHash = "fingerprint",
                FeedRange = new FeedRangePartitionKeyRange("0")
            };
            DocumentServiceLease incompatible = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", Owner = "host", QueryFilterHash = "other", ContinuationToken = "server-progress"
            };
            int replacements = 0;
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new CosmosJsonDotNetSerializer().ToStream(replacements == 0 ? lease : incompatible)
                });
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    replacements++;
                    return new ResponseMessage(HttpStatusCode.PreconditionFailed);
                });
            DocumentServiceLeaseManagerCosmos manager = new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(), container.Object, new DocumentServiceLeaseUpdaterCosmos(container.Object),
                new DocumentServiceLeaseStoreManagerOptions { HostName = "host" }, new PartitionedByIdCollectionRequestOptionsFactory());
            manager.ConfigureQueryFilter("fingerprint");

            await AssertQueryMismatchAsync(manager, operation, lease);

            Assert.AreEqual(1, replacements);
            Assert.AreEqual("other", incompatible.QueryFilterHash);
            Assert.AreEqual("host", incompatible.Owner);
            Assert.AreEqual("server-progress", incompatible.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task RejectedAcquisitionCleanupDoesNotReleaseOriginalOwner(bool inMemory)
        {
            DocumentServiceLease lease = new DocumentServiceLeaseCore
            {
                LeaseId = "0", LeaseToken = "0", Owner = "original-owner", QueryFilterHash = "other",
                ContinuationToken = "original-progress", FeedRange = new FeedRangePartitionKeyRange("0")
            };
            ConcurrentDictionary<string, DocumentServiceLease> state = new ConcurrentDictionary<string, DocumentServiceLease>();
            state.TryAdd(lease.Id, lease);
            int updates = 0;
            Mock<DocumentServiceLeaseUpdater> updater = new Mock<DocumentServiceLeaseUpdater>();
            updater.Setup(c => c.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(), It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .Returns((DocumentServiceLease cached, string id, PartitionKey pk, Func<DocumentServiceLease, DocumentServiceLease> update) =>
                {
                    if (++updates == 1)
                    {
                        throw new ArgumentException("Simulated acquisition rejection.");
                    }

                    return Task.FromResult(update(lease));
                });
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK) { Content = new CosmosJsonDotNetSerializer().ToStream(lease) });
            DocumentServiceLeaseManager manager = inMemory
                ? new DocumentServiceLeaseManagerInMemory(updater.Object, state)
                : CreateLeaseManager(container.Object, new PartitionedByIdCollectionRequestOptionsFactory(), updater.Object);
            manager.ConfigureQueryFilter("configured");
            Mock<ChangeFeedProcessorHealthMonitor> monitor = new Mock<ChangeFeedProcessorHealthMonitor>();
            monitor.Setup(m => m.NotifyErrorAsync(It.IsAny<string>(), It.IsAny<Exception>())).Returns(Task.CompletedTask);
            monitor.Setup(m => m.NotifyLeaseReleaseAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            Mock<PartitionSupervisorFactory> supervisors = new Mock<PartitionSupervisorFactory>(MockBehavior.Strict);
            PartitionControllerCore controller = new PartitionControllerCore(
                Mock.Of<DocumentServiceLeaseContainer>(), manager, supervisors.Object, Mock.Of<PartitionSynchronizer>(), monitor.Object);

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => controller.AddOrUpdateLeaseAsync(lease));

            Assert.AreEqual("original-owner", lease.Owner);
            Assert.AreEqual("original-progress", lease.ContinuationToken);
            Assert.AreEqual("other", lease.QueryFilterHash);
            supervisors.VerifyNoOtherCalls();
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.NotFound, (int)Documents.SubStatusCodes.Unknown)]
        [DataRow(HttpStatusCode.NotFound, (int)Documents.SubStatusCodes.ReadSessionNotAvailable)]
        [DataRow(HttpStatusCode.TooManyRequests, (int)Documents.SubStatusCodes.Unknown)]
        public async Task CreateConflictOnlyIgnoresDeletedLease(HttpStatusCode status, int substatus)
        {
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.Conflict));
            container.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    ResponseMessage response = new ResponseMessage(status);
                    response.Headers.SubStatusCode = (Documents.SubStatusCodes)substatus;
                    return response;
                });
            DocumentServiceLeaseManagerCosmos manager = CreateLeaseManager(
                container.Object, new PartitionedByIdCollectionRequestOptionsFactory());

            if (status == HttpStatusCode.NotFound && substatus == 0)
            {
                Assert.IsNull(await CreateLeaseAsync(manager, "PKRange", "progress"));
            }
            else
            {
                CosmosException error = await Assert.ThrowsExceptionAsync<CosmosException>(() => CreateLeaseAsync(manager, "PKRange", "progress"));
                Assert.AreEqual(status, error.StatusCode);
                Assert.AreEqual(substatus, error.SubStatusCode);
            }
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

        private static Mock<ContainerInternal> CreatePkCapturingMockContainer(Action<PartitionKey> onCapture)
        {
            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback((Stream stream, PartitionKey partitionKey, ItemRequestOptions opts, CancellationToken token) => onCapture(partitionKey))
                .ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions opts, CancellationToken token)
                    => new ResponseMessage(HttpStatusCode.OK) { Content = stream });
            return mockedContainer;
        }

        private static Mock<ContainerInternal> CreateDedupSimulatingMockContainer()
        {
            ConcurrentDictionary<string, string> createdPkIdPairs = new ConcurrentDictionary<string, string>();
            Mock<ContainerInternal> mockedContainer = new Mock<ContainerInternal>();
            mockedContainer.Setup(c => c.CreateItemStreamAsync(
                It.IsAny<Stream>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey partitionKey, ItemRequestOptions opts, CancellationToken token) =>
                {
                    stream.Position = 0;
                    using StreamReader reader = new StreamReader(stream, leaveOpen: true);
                    string json = reader.ReadToEnd();
                    stream.Position = 0;
                    string leaseId = Newtonsoft.Json.Linq.JObject.Parse(json)["id"].ToString();

                    string compositeKey = $"{partitionKey}:{leaseId}";
                    return createdPkIdPairs.TryAdd(compositeKey, json)
                        ? new ResponseMessage(HttpStatusCode.OK) { Content = stream }
                        : new ResponseMessage(HttpStatusCode.Conflict);
                });
            mockedContainer.Setup(c => c.ReadItemStreamAsync(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions opts, CancellationToken token) =>
                    createdPkIdPairs.TryGetValue($"{partitionKey}:{id}", out string json)
                        ? new ResponseMessage(HttpStatusCode.OK) { Content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)) }
                        : new ResponseMessage(HttpStatusCode.NotFound));
            return mockedContainer;
        }

        private static DocumentServiceLeaseManagerCosmos CreateLeaseManager(
            ContainerInternal leaseContainer,
            RequestOptionsFactory requestOptionsFactory,
            DocumentServiceLeaseUpdater updater = null)
        {
            return new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                leaseContainer,
                updater ?? Mock.Of<DocumentServiceLeaseUpdater>(),
                new DocumentServiceLeaseStoreManagerOptions { HostName = Guid.NewGuid().ToString() },
                requestOptionsFactory);
        }

        private static Task<DocumentServiceLease> CreateLeaseAsync(
            DocumentServiceLeaseManagerCosmos manager,
            string overloadType,
            string continuation)
        {
            return overloadType == "PKRange"
                ? manager.CreateLeaseIfNotExistAsync(
                    new Documents.PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" },
                    continuation)
                : manager.CreateLeaseIfNotExistAsync(
                    new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)),
                    continuation);
        }

        private static DocumentServiceLeaseCore CreatePreExistingGuidPkLease(
            out string guidPartitionKey,
            out DocumentServiceLeaseStoreManagerOptions options)
        {
            guidPartitionKey = Guid.NewGuid().ToString();
            string hostName = Guid.NewGuid().ToString();
            options = new DocumentServiceLeaseStoreManagerOptions { HostName = hostName };
            return new DocumentServiceLeaseCore
            {
                LeaseId = "some-prefix..0",
                LeaseToken = "0",
                Owner = hostName,
                LeasePartitionKey = guidPartitionKey,
                FeedRange = new FeedRangePartitionKeyRange("0")
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
