//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SessionContainer = Microsoft.Azure.Cosmos.Common.SessionContainer;

    /// <summary>
    /// Tests for <see cref="StoreReader"/>
    /// </summary>
    [TestClass]
    public class DirectStoreReaderTests
    {


        /// <summary>
        /// Tests for <see cref="AddressSelector"/>
        /// </summary>
        [TestMethod]
        [Owner("dikshibahl")]
        public async Task VerifyAddressSelectorBackgroundRefreshClonesDocumentServiceRequestAsync()
        {
            // create a real document service request
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            var requestContext = new DocumentServiceRequestContext();
            requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);

            // The force refresh should not use the same document service request has the passed in one
            // to avoid any concurrency issues since document service request is not thread safe.
            mockAddressResolver.Setup(x => x.ResolveAsync(
                It.Is<DocumentServiceRequest>(d => !object.ReferenceEquals(entity, d) && !object.ReferenceEquals(entity.Headers, d.Headers)),
                true, // force refresh
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult((PartitionAddressInformation)null));

            AddressSelector addressSelector = new AddressSelector(mockAddressResolver.Object, Protocol.Tcp);
            addressSelector.StartBackgroundAddressRefresh(entity);

            await DirectStoreReaderTests.VerifyBackgroundRefreshHelpAsync(mockAddressResolver);
        }

        [TestMethod]
        [Owner("askagarw")]
        [DataRow(false, DisplayName = "Store reader gone exception test with replica validation disabled.")]
        [DataRow(true, DisplayName = "Store reader gone exception test with replica validation enabled.")]
        public async Task StoreReaderGoneExceptionHasSubStatusCodeAsync(bool replicaValidationEnabled)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            var requestContext = new DocumentServiceRequestContext();
            requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary: true,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new HashSet<TransportAddressUri>(addressInformation.Select(x => new TransportAddressUri(new Uri(x.PhysicalUri))));

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);

            TransportException transportException = new TransportException(TransportErrorCode.ReceiveFailed,
                                                                            null,
                                                                            Guid.NewGuid(),
                                                                            new Uri(addressInformation[0].PhysicalUri),
                                                                            "Source Description",
                                                                            true,
                                                                            true);

            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(
            It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
            entity))
            .ThrowsAsync(Rntbd.TransportExceptions.GetGoneException(new Uri(addressInformation[0].PhysicalUri), Guid.NewGuid(), transportException));

            StoreReader storeReader = new StoreReader(
                        mockTransportClient.Object,
                        new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                        new AddressEnumerator(),
                        sessionContainer,
                        replicaValidationEnabled);

            DocumentClientException dce = await Assert.ThrowsExceptionAsync<GoneException>(() =>
                storeReader.ReadMultipleReplicaAsync(entity, true, 1, true, false, ReadMode.Any));
            Assert.AreEqual(dce.StatusCode, HttpStatusCode.Gone);
            Assert.AreEqual(dce.GetSubStatus(), SubStatusCodes.TransportGenerated410);
        }

        [TestMethod]
        [Owner("askagarw")]
        [DataRow(false, DisplayName = "Store reader capture store result diagnostics test with replica validation disabled.")]
        [DataRow(true, DisplayName = "Store reader capture store result diagnostics test with replica validation enabled.")]
        public async Task StoreReaderCapturesStoreResultDiagnosticsForSplits(bool replicaValidationEnabled)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            var requestContext = new DocumentServiceRequestContext();
            ClientSideRequestStatistics clientSideRequestStatistics = new ClientSideRequestStatistics();
            requestContext.ClientRequestStatistics = clientSideRequestStatistics;
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary:  true,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new HashSet<TransportAddressUri>(addressInformation.Select(x => new TransportAddressUri(new Uri(x.PhysicalUri))));

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);

            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(
            It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
            entity))
            .ThrowsAsync(new PartitionKeyRangeIsSplittingException());

            StoreReader storeReader = new StoreReader(
                        mockTransportClient.Object,
                        new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                        new AddressEnumerator(),
                        sessionContainer,
                        replicaValidationEnabled);

            // Store Reader should throw the exception but should also capture the exception in the ClientSideRequestStats object
            DocumentClientException dce = await Assert.ThrowsExceptionAsync<PartitionKeyRangeIsSplittingException>(() =>
                storeReader.ReadMultipleReplicaAsync(entity, true, 1, true, false, ReadMode.Any));
            List<ClientSideRequestStatistics.StoreResponseStatistics> storeResultList =
                (List<ClientSideRequestStatistics.StoreResponseStatistics>)typeof(ClientSideRequestStatistics).GetField("responseStatisticsList", BindingFlags.NonPublic | BindingFlags.Instance)
                                               .GetValue(clientSideRequestStatistics);
            Assert.IsTrue(storeResultList[0].StoreResult.Exception is PartitionKeyRangeIsSplittingException);

            dce = await Assert.ThrowsExceptionAsync<PartitionKeyRangeIsSplittingException>(() =>
                storeReader.ReadPrimaryAsync(entity, false, false));
            Assert.IsTrue(storeResultList[1].StoreResult.Exception is PartitionKeyRangeIsSplittingException);
        }

        [TestMethod]
        [Owner("askagarw")]
        [DataRow(false, DisplayName = "Store reader failed replicas test with replica validation disabled.")]
        [DataRow(true, DisplayName = "Store reader failed replicas test with replica validation enabled.")]
        public async Task StoreReaderFailedReplicasTestAsync(bool replicaValidationEnabled)
        {
            DefaultTrace.InitEventListener();

            // create a real document service request
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            var requestContext = new DocumentServiceRequestContext();
            requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary:  true,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/1111s",
                    isPrimary:  false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/2222s",
                    isPrimary:  false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new HashSet<TransportAddressUri>(addressInformation.Select(x => new TransportAddressUri(new Uri(x.PhysicalUri))));

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);

            TransportException transportException = new TransportException(TransportErrorCode.ReceiveFailed,
                                                                            null,
                                                                            Guid.NewGuid(),
                                                                            new Uri(addressInformation[0].PhysicalUri),
                                                                            "Source Description",
                                                                            true,
                                                                            true);

            bool throwOnce = false;
            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(
                It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
                entity))
            .Returns(() =>
            {
                if (!throwOnce)
                {
                    throwOnce = true;
                    throw Rntbd.TransportExceptions.GetGoneException(new Uri(addressInformation[0].PhysicalUri), Guid.NewGuid(), transportException);
                }
                else
                {
                    return Task.FromResult(new StoreResponse());
                }
            });

            StoreReader storeReader = new StoreReader(
                mockTransportClient.Object,
                new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                new AddressEnumerator(),
                sessionContainer,
                replicaValidationEnabled);

            // ReadMultipleReplicaAsync for Eventual Consistency
            await storeReader.ReadMultipleReplicaAsync(entity, true, 1, false, false, ReadMode.Any);

            // Assert Failed Endpoint list is being updated
            Assert.AreEqual(entity.RequestContext.FailedEndpoints.Value.Count, 1);

            // ReadMultipleReplicaAsync which requires all 3 replicas. To test the order in which these replicas are called
            IList<ReferenceCountedDisposable<StoreResult>> storeResults = (await storeReader.ReadMultipleReplicaAsync(entity, true, 3, false, false, ReadMode.Any));
            // Test the failed replica is tried at the end
            Assert.AreEqual(storeResults.Count, 3);
            string failedReplicaUri = entity.RequestContext.FailedEndpoints.Value.First().Key.Uri.OriginalString;
            Assert.AreNotEqual(storeResults[0].Target.StorePhysicalAddress.OriginalString,
                                failedReplicaUri);
            Assert.AreNotEqual(storeResults[1].Target.StorePhysicalAddress.OriginalString,
                                failedReplicaUri);
            Assert.AreEqual(storeResults[2].Target.StorePhysicalAddress.OriginalString,
                            failedReplicaUri);
        }

        private sealed class TestTraceListener : TraceListener
        {
            public Action<string> Callback { get; set; }
            public override bool IsThreadSafe => true;
            public override void Write(string message) => Callback(message);
            public override void WriteLine(string message) => Callback(message);
        }

        [TestMethod]
        [Owner("askagarw")]
        [DataRow(false, DisplayName = "Store reader address refresh test with replica validation disabled.")]
        [DataRow(true, DisplayName = "Store reader address refresh test with replica validation enabled.")]
        public async Task VerifyStoreReaderAddressRefreshOnCancellationTokenAsync(bool replicaValidationEnabled)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            var requestContext = new DocumentServiceRequestContext();
            requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            // Create Timeout helper with cancellation token
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2), cancellationTokenSource.Token);

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary: true,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/1111s",
                    isPrimary: false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/2222s",
                    isPrimary: false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new HashSet<TransportAddressUri>(addressInformation.Select(x => new TransportAddressUri(new Uri(x.PhysicalUri))));

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // For Address Refresh
            mockAddressResolver.Setup(x => x.ResolveAsync(
                It.Is<DocumentServiceRequest>(d => !object.ReferenceEquals(entity, d) && !object.ReferenceEquals(entity.Headers, d.Headers)),
                true,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);

            // Throw a gone exception and also cancel the CT
            mockTransportClient.SetupSequence(x => x.InvokeResourceOperationAsync(
                        It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
                        entity))
                .Returns(() =>
                {
                    cancellationTokenSource.Cancel(); // Cancel CT to test if we perform address refresh
                    throw new GoneException("Mock gone exception", SubStatusCodes.Unknown);
                });

            StoreReader storeReader =
                new StoreReader(
                    mockTransportClient.Object,
                    new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                    new AddressEnumerator(),
                    sessionContainer,
                    replicaValidationEnabled);

            // Throws OperationCanceledException because Cancellation Requested
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => storeReader.ReadMultipleReplicaAsync(
                    entity: entity,
                    includePrimary: false,
                    replicaCountToRead: 1,
                    requiresValidLsn: true,
                    useSessionToken: false,
                    readMode: ReadMode.Any));

            // But Background refresh is performed
            await DirectStoreReaderTests.VerifyBackgroundRefreshHelpAsync(mockAddressResolver);
            cancellationTokenSource.Dispose();
        }

        /// <summary>
        /// Tests for <see cref="TransportClient"/>
        /// </summary>
        [TestMethod]
        [Owner("dikshibahl")]
        [DataRow(false, DisplayName = "Store reader background address refresh from 410 test with replica validation disabled.")]
        [DataRow(true, DisplayName = "Store reader background address refresh from 410 test with replica validation enabled.")]
        public async Task VerifyStoreReaderBackgroundAddressRefreshFrom410IsThreadSafeAsync(bool replicaValidationEnabled)
        {
            // create a real document service request
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            var requestContext = new DocumentServiceRequestContext();
            requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary:  true,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/1111s",
                    isPrimary:  false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/2222s",
                    isPrimary:  false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new HashSet<TransportAddressUri>(addressInformation.Select(x => new TransportAddressUri(new Uri(x.PhysicalUri))));

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // The force refresh should not use the same document service request has the passed in one
            // to avoid any concurrency issues since document service request is not thread safe.
            mockAddressResolver.Setup(x => x.ResolveAsync(
                It.Is<DocumentServiceRequest>(d => !object.ReferenceEquals(entity, d) && !object.ReferenceEquals(entity.Headers, d.Headers)),
                true,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);

            // Document service request should be the same and the address must be from the mocked list
            // Throw a gone exception which will trigger a background address refresh.
            mockTransportClient.SetupSequence(x => x.InvokeResourceOperationAsync(
                        It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
                        entity))
                .Returns(() => throw new GoneException("Mock gone exception", SubStatusCodes.Unknown))
                .Returns(() => Task.FromResult(new StoreResponse()
                {
                    Headers = new StoreResponseNameValueCollection()
                    {
                        LSN = "90",
                        ActivityId = "ACTIVITYID1_1",
                        GlobalCommittedLSN = "90",
                        NumberOfReadRegions = "1",
                    }
                }));

            // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
            StoreReader storeReader =
                new StoreReader(
                    mockTransportClient.Object,
                    new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                    new AddressEnumerator(),
                    sessionContainer,
                    replicaValidationEnabled);

            var result = await storeReader.ReadMultipleReplicaAsync(
                    entity: entity,
                    includePrimary: false /*includePrimary*/,
                    replicaCountToRead: 1,
                    requiresValidLsn: true /*requiresValidLSN*/ ,
                    useSessionToken: false /*useSessionToken*/,
                    readMode: ReadMode.Any);

            // make sure we got 1 responses from the store reader
            Assert.AreEqual(1, result.Count);
            mockTransportClient.VerifyAll();
            await DirectStoreReaderTests.VerifyBackgroundRefreshHelpAsync(mockAddressResolver);
        }

        private static async Task VerifyBackgroundRefreshHelpAsync(Mock<IAddressResolver> mockAddressResolver)
        {
            // The background refresh may take some time to start.
            // Loop until the request is verified or it times out.
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                    mockAddressResolver.VerifyAll();
                    return;
                }
                catch (Exception)
                {
                    if (stopwatch.Elapsed > TimeSpan.FromSeconds(1))
                    {
                        throw;
                    }
                }
            } while (true);
        }

        private TransportClient GetMockTransportClientDuringUpgrade(AddressInformation[] addressInformation)
        {
            // create objects for all the dependencies of the StoreReader
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // create mock store response object
            StoreResponse mockStoreResponseFast = new StoreResponse();
            StoreResponse mockStoreResponseSlow = new StoreResponse();

            // set lsn and activityid on the store response.
            mockStoreResponseFast.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "50"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                });

            // set lsn and activityid on the store response.
            mockStoreResponseSlow.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "30"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                });

            // setup mock transport client for the first replica
            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[0].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponseFast);
            mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(mockStoreResponseFast);

            // setup mock transport client with a sequence of outputs
            mockTransportClient.SetupSequence(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[1].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // initial read response
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // barrier retry, count 1
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // barrier retry, count 2
                    .Throws(new InvalidPartitionException())                          // throw invalid partition exception to simulate collection recreate with same name
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // new read
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // subsequent barriers
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast));
            mockTransportClient.SetupSequence(
                    client => client.InvokeResourceOperationAsync(
                        new TransportAddressUri(new Uri(addressInformation[1].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // initial read response
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // barrier retry, count 1
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // barrier retry, count 2
                .Throws(new InvalidPartitionException())                          // throw invalid partition exception to simulate collection recreate with same name
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // new read
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))   // subsequent barriers
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast))
                .Returns(Task.FromResult<StoreResponse>(mockStoreResponseFast));

            // After this, the product code should reset target identity, and lsn response

            var queueOfResponses = new Queue<StoreResponse>();

            // let the first 10 responses be slow, and then fast
            for (int i = 0; i < 20; i++)
            {
                queueOfResponses.Enqueue(i <= 2 ? mockStoreResponseSlow : mockStoreResponseFast);
            }

            // setup mock transport client with a sequence of outputs, for the second replica
            // This replica behaves in the following manner:
            // calling InvokeResourceOperationAsync
            // 1st time: returns valid LSN
            // 2nd time: returns InvalidPartitionException
            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[2].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(queueOfResponses.Dequeue());
            mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new TransportAddressUri(new Uri(addressInformation[2].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(queueOfResponses.Dequeue());

            TransportClient mockTransportClientObject = mockTransportClient.Object;

            return mockTransportClientObject;
        }

        private enum ReadQuorumResultKind
        {
            QuorumMet,
            QuorumSelected,
            QuorumNotSelected
        }

        private TransportClient GetMockTransportClientForGlobalStrongReads(AddressInformation[] addressInformation, ReadQuorumResultKind result)
        {
            // create objects for all the dependencies of the StoreReader
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // create mock store response object
            StoreResponse mockStoreResponse1 = new StoreResponse();
            StoreResponse mockStoreResponse2 = new StoreResponse();
            StoreResponse mockStoreResponse3 = new StoreResponse();
            StoreResponse mockStoreResponse4 = new StoreResponse();
            StoreResponse mockStoreResponse5 = new StoreResponse();

            // set lsn and activityid on the store response.
            mockStoreResponse1.Headers = new DictionaryNameValueCollection(new NameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            });

            mockStoreResponse2.Headers = new DictionaryNameValueCollection(new NameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "90"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_2" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            });

            mockStoreResponse3.Headers = new DictionaryNameValueCollection(new NameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "92"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            });

            mockStoreResponse4.Headers = new DictionaryNameValueCollection(new NameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "92" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            });

            mockStoreResponse5.Headers = new DictionaryNameValueCollection(new NameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "1" },
                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
            });

            if (result == ReadQuorumResultKind.QuorumMet)
            {
                // setup mock transport client for the first replica
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new Uri(addressInformation[0].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse5);
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse5);

                mockTransportClient.SetupSequence(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[1].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse5));
                mockTransportClient.SetupSequence(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[1].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse5));

                mockTransportClient.SetupSequence(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[2].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse4))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse5));
                mockTransportClient.SetupSequence(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[2].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse4))
                    .Returns(Task.FromResult<StoreResponse>(mockStoreResponse5));

            }
            if (result == ReadQuorumResultKind.QuorumSelected)
            {
                // setup mock transport client for the first replica
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new Uri(addressInformation[0].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse2);
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse2);

                // setup mock transport client with a sequence of outputs
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new Uri(addressInformation[1].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse1);
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[1].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse1);

                // setup mock transport client with a sequence of outputs
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new Uri(addressInformation[2].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse2);
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[2].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse2);
            }
            else if (result == ReadQuorumResultKind.QuorumNotSelected)
            {
                // setup mock transport client for the first replica
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new Uri(addressInformation[0].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse5);
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse5);

                mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[1].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse5);
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[1].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse5);

                mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new Uri(addressInformation[2].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                    .Throws(new GoneException("test", SubStatusCodes.Unknown));
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[2].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .Throws(new GoneException("test", SubStatusCodes.Unknown));
            }

            TransportClient mockTransportClientObject = mockTransportClient.Object;

            return mockTransportClientObject;
        }

        private TransportClient GetMockTransportClientForGlobalStrongWrites(
            AddressInformation[] addressInformation,
            int indexOfCaughtUpReplica,
            bool undershootGlobalCommittedLsnDuringBarrier,
            bool overshootLsnDuringBarrier,
            bool overshootGlobalCommittedLsnDuringBarrier)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // create mock store response object
            StoreResponse mockStoreResponse1 = new StoreResponse();
            StoreResponse mockStoreResponse2 = new StoreResponse();
            StoreResponse mockStoreResponse3 = new StoreResponse();
            StoreResponse mockStoreResponse4 = new StoreResponse();
            StoreResponse mockStoreResponse5 = new StoreResponse();


            // set lsn and activityid on the store response.
            mockStoreResponse1.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "100"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                });

            mockStoreResponse2.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "100"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_2" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                });

            mockStoreResponse3.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "103"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                });

            mockStoreResponse4.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "103"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "103" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                });

            mockStoreResponse5.Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "106"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "103" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                });
            StoreResponse finalResponse = null;
            if (undershootGlobalCommittedLsnDuringBarrier)
            {
                finalResponse = mockStoreResponse1;
            }
            else
            {
                if (overshootLsnDuringBarrier)
                {
                    if (overshootGlobalCommittedLsnDuringBarrier)
                    {
                        finalResponse = mockStoreResponse5;
                    }
                    else
                    {
                        finalResponse = mockStoreResponse3;
                    }
                }
                else
                {
                    if (overshootGlobalCommittedLsnDuringBarrier)
                    {
                        finalResponse = mockStoreResponse4;
                    }
                    else
                    {
                        finalResponse = mockStoreResponse2;
                    }
                }
            }

            for (int i = 0; i < addressInformation.Length; i++)
            {
                if (i == indexOfCaughtUpReplica)
                {
                    mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(new Uri(addressInformation[i].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .Returns(Task.FromResult<StoreResponse>(finalResponse));
                    mockTransportClient.Setup(
                            client => client.InvokeResourceOperationAsync(new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .Returns(Task.FromResult<StoreResponse>(finalResponse));
                }
                else
                {
                    mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1));
                }
            }

            return mockTransportClient.Object;
        }

        /// <summary>
        /// We are simulating upgrade scenario where one of the secondary replicas is down.
        /// And one of the other secondary replicas is an XP Primary (lagging behind).
        /// Dyanmic Quorum is in effect, so Write Quorum = 2
        /// </summary>
        /// <returns> array of AddressInformation </returns>
        private AddressInformation[] GetMockAddressInformationDuringUpgrade(int count = 3)
        {
            // setup mocks for address information
            AddressInformation[] addressInformation = new AddressInformation[count];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i < count; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }
            return addressInformation;
        }

        /// <summary>
        /// Given an array of address information, gives mock address cache.
        /// </summary>
        /// <param name="addressInformation"></param>
        /// <returns></returns>
        private Mock<IAddressResolver> GetMockAddressCache(AddressInformation[] addressInformation)
        {
            // Address Selector is an internal sealed class that can't be mocked, but its dependency
            // AddressCache can be mocked.
            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),/*forceRefresh*/
                    new CancellationToken()))
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            return mockAddressCache;
        }

        /// <summary>
        /// Tests for <see cref="StoreReader"/>
        /// </summary>
        [TestMethod]
        [Owner("dikshibahl")]
        public void StoreResultToStringTest()
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.QuorumAckedLocalLSN, "100"},
                { WFConstants.BackendHeaders.CurrentWriteQuorum, "1"},
                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "4"},
                { HttpConstants.HttpHeaders.RequestCharge, "42.25"},
                { WFConstants.BackendHeaders.ItemLocalLSN, "80"},
                { WFConstants.BackendHeaders.LocalLSN, "85"},
                { HttpConstants.HttpHeaders.SessionToken, "90"},
                { HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, "9000"},
                { HttpConstants.HttpHeaders.ActivityId, "ACTIVITYID1_1" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                { HttpConstants.HttpHeaders.Continuation, "+RID:~9lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=" }
            };

            StoreResponse storeResponse = new StoreResponse()
            {
                Status = 200,
                Headers = headers,
            };

            List<string> replicaHealthStatuses = new List<string>()
            {
                "http://storephysicaladdress-1p.com:Connected",
                "http://storephysicaladdress-2s.com:Unknown",
                "http://storephysicaladdress-3s.com:Unhealthy",
                "http://storephysicaladdress-4s.com:Unknown"
            };

            Uri uri = new Uri("http://dummyTargetIdentity1");
            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateStoreResult(
                storeResponse: storeResponse,
                responseException: null,
                requiresValidLsn: false,
                useLocalLSNBasedHeaders: false,
                replicaHealthStatuses: replicaHealthStatuses,
                storePhysicalAddress: uri);

            string diagnostics = storeResult.Target.ToString();
            StringBuilder stringBuilder = new StringBuilder();
            storeResult.Target.AppendToBuilder(stringBuilder);
            string resultStringFromStringBuilder = stringBuilder.ToString();
            Assert.AreEqual(diagnostics, resultStringFromStringBuilder);

            Assert.IsTrue(diagnostics.Contains("StorePhysicalAddress: " + uri));
            Assert.IsTrue(diagnostics.Contains("LSN: " + headers.LSN));
            Assert.IsTrue(diagnostics.Contains("GlobalCommittedLsn: " + headers.GlobalCommittedLSN));
            Assert.IsTrue(diagnostics.Contains("PartitionKeyRangeId: " + headers.PartitionKeyRangeId));
            Assert.IsTrue(diagnostics.Contains("IsValid: True"));
            Assert.IsTrue(diagnostics.Contains("StatusCode: 200"));
            Assert.IsTrue(diagnostics.Contains("SubStatusCode: 0"));
            Assert.IsTrue(diagnostics.Contains("RequestCharge: " + headers.RequestCharge));
            Assert.IsTrue(diagnostics.Contains("ItemLSN: -1"));
            Assert.IsTrue(diagnostics.Contains("SessionToken: " + headers.SessionToken));
            Assert.IsTrue(diagnostics.Contains("UsingLocalLSN: False"));
            Assert.IsTrue(diagnostics.Contains("TransportException: null"));
            Assert.IsTrue(diagnostics.Contains("BELatencyMs: " + headers.BackendRequestDurationMilliseconds));
            Assert.IsTrue(diagnostics.Contains("ActivityId: " + headers.ActivityId));
            Assert.IsTrue(diagnostics.Contains("ReplicaHealthStatuses: [" + replicaHealthStatuses[0]));
        }


        /// <summary>
        /// Tests for <see cref="StoreReader"/>
        /// </summary>
        [TestMethod]
        [Owner("dikshibahl")]
        [DataRow(false, DisplayName = "Store reader continuation token test with replica validation disabled.")]
        [DataRow(true, DisplayName = "Store reader continuation token test with replica validation enabled.")]
        public async Task StoreReaderContinuationTokenTestAsync(bool replicaValidationEnabled)
        {
            string expectedContinuationToken = "+RID:~6lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=";
            List<string> continuationTokens = new List<string>()
            {
                expectedContinuationToken,
                null,
                "+RID:~6lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=;",
                "+RID:~6lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=;+RID:~6lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=;+RID:~6lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=;"
            };

            foreach (string continuationToken in continuationTokens)
            {
                // create a real document service request
                DocumentServiceRequest entity = DocumentServiceRequest.Create(
                    operationType: OperationType.Query,
                    resourceType: ResourceType.Document,
                    authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

                entity.Continuation = continuationToken;

                // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
                var requestContext = new DocumentServiceRequestContext();
                requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
                requestContext.RequestChargeTracker = new RequestChargeTracker();
                entity.RequestContext = requestContext;

                // also setup timeout helper, used in store reader
                entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

                // when the store reader throws Invalid Partition exception, the higher layer should
                // clear this target identity.
                entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
                entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

                AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade();
                var mockAddressCache = GetMockAddressCache(addressInformation);

                // validate that the mock works
                PartitionAddressInformation partitionAddressInformation = await mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken());
                var addressInfo = partitionAddressInformation.AllAddresses;

                Assert.IsTrue(addressInfo[0] == addressInformation[0]);

                AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
                Uri primaryAddress = (await addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/))?.Uri;

                // check if the address return from Address Selector matches the original address info
                Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

                // create objects for all the dependencies of the StoreReader
                Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

                // setup mock transport client for the first replica
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        It.IsAny<Uri>(),
                        entity))
                    .Callback<Uri, DocumentServiceRequest>((uri, dsr) =>
                    {
                        if (dsr.Continuation != null)
                        {
                            Assert.IsTrue(string.Equals(expectedContinuationToken, dsr.Continuation, StringComparison.Ordinal));
                        }
                    })
                    .ReturnsAsync(() =>
                    {
                        StoreResponse mockStoreResponse = new StoreResponse();
                        mockStoreResponse.Headers = new StoreResponseNameValueCollection()
                        {
                            { WFConstants.BackendHeaders.LSN, "100"},
                            { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                            { HttpConstants.HttpHeaders.Continuation, "+RID:~9lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=" }
                        };

                        return mockStoreResponse;
                    });
                mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            It.IsAny<TransportAddressUri>(),
                            entity))
                    .Callback<TransportAddressUri, DocumentServiceRequest>((uri, dsr) =>
                    {
                        if (dsr.Continuation != null)
                        {
                            Assert.IsTrue(string.Equals(expectedContinuationToken, dsr.Continuation, StringComparison.Ordinal));
                        }
                    })
                    .ReturnsAsync(() =>
                    {
                        StoreResponse mockStoreResponse = new StoreResponse();
                        mockStoreResponse.Headers = new StoreResponseNameValueCollection()
                        {
                            { WFConstants.BackendHeaders.LSN, "100"},
                            { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                            { HttpConstants.HttpHeaders.Continuation, "+RID:~9lALAKW5J-UBAAAAAAAAAA==#RT:1#TRC:1#ISV:2#IEO:65551#FPC:AQEAAAAAAAAAAwAAAAAAAAA=" }
                        };

                        return mockStoreResponse;
                    });

                // create a real session container - we don't need session for this test anyway
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);

                // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
                StoreReader storeReader =
                    new StoreReader(mockTransportClient.Object,
                        addressSelector,
                        new AddressEnumerator(),
                        sessionContainer,
                        replicaValidationEnabled);

                // reads always go to read quorum (2) replicas
                int replicaCountToRead = 2;

                try
                {
                    IList<ReferenceCountedDisposable<StoreResult>> result = await storeReader.ReadMultipleReplicaAsync(
                        entity,
                        false /*includePrimary*/,
                        replicaCountToRead,
                        true /*requiresValidLSN*/ ,
                        false /*useSessionToken*/,
                        ReadMode.Strong);

                    // make sure we got 2 responses from the store reader
                    Assert.IsTrue(result.Count == 2);
                }
                catch (BadRequestException be)
                {
                    int semicolonCount = continuationToken.Count(x => x == ';');
                    Assert.IsTrue(semicolonCount > 0 && semicolonCount < 3);
                    Assert.IsTrue(be.ToString().Contains(continuationToken));
                }
            }
        }


        [TestMethod]
        [Owner("dibilla")]
        [DataRow(true, "Strong", "Eventual", true)]
        [DataRow(false, "", "Eventual", false)]
        [DataRow(true, "Eventual", "Strong", false)]
        [DataRow(false, "", "Strong", true)]
        public void TestConsistencyLevelOverrideIsHonoredRequest(
            bool isConsistencyLevelOverriden,
            string overridenConsistencyLevelString,
            string defaultConsistencyLevelString,
            bool expectedStrongWrite)
        {
            AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade();
            var mockAddressCache = GetMockAddressCache(addressInformation);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReaderExtension> mockServiceConfigReader = new Mock<IServiceConfigurationReaderExtension>();

            ConsistencyLevel consistencyLevel = (ConsistencyLevel)(-1);
            if (isConsistencyLevelOverriden)
            {
                _ = Enum.TryParse(overridenConsistencyLevelString, out consistencyLevel);
            }

            _ = Enum.TryParse(defaultConsistencyLevelString, out ConsistencyLevel defaultConsistencyLevel);
            mockServiceConfigReader.Setup(x => x.DefaultConsistencyLevel).Returns(defaultConsistencyLevel);
            mockServiceConfigReader.Setup(x => x.TryGetConsistencyLevel(It.IsAny<DocumentServiceRequest>(), out consistencyLevel))
                .Returns(isConsistencyLevelOverriden);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            var mockTransportClient = GetMockTransportClientForGlobalStrongWrites(addressInformation, 0, false, false, false);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);

            ReferenceCountedDisposable<StoreResult> newStoreResult = StoreResult.CreateForTesting(new TransportRequestStats());

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);
            Assert.AreEqual(expectedStrongWrite, consistencyWriter.ShouldPerformWriteBarrierForGlobalStrong(newStoreResult.Target, entity));
        }

        /// <summary>
        /// Verifies that QuorumReader honors TryGetConsistencyLevel override for global strong reads.
        /// When TryGetConsistencyLevel overrides to Strong on a non-Strong account, the global strong
        /// read path should activate (requiring globalCommittedLSN >= maxLSN for quorum).
        /// </summary>
        [TestMethod]
        [Owner("dibilla")]
        [DataRow(true, "Strong", "Eventual", true, DisplayName = "Override to Strong on Eventual account => global strong read")]
        [DataRow(false, "", "Eventual", false, DisplayName = "No override on Eventual account => no global strong read")]
        [DataRow(false, "", "Strong", true, DisplayName = "No override on Strong account => global strong read (default)")]
        public void TestConsistencyLevelOverrideIsHonoredForGlobalStrongReads(
            bool isConsistencyLevelOverridden,
            string overriddenConsistencyLevelString,
            string defaultConsistencyLevelString,
            bool expectedGlobalStrongRead)
        {
            AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade();
            var mockAddressCache = GetMockAddressCache(addressInformation);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            // Set up IServiceConfigurationReaderExtension mock (inherits IServiceConfigurationReader)
            Mock<IServiceConfigurationReaderExtension> mockServiceConfigReader = new Mock<IServiceConfigurationReaderExtension>();

            ConsistencyLevel consistencyLevel = (ConsistencyLevel)(-1);
            if (isConsistencyLevelOverridden)
            {
                _ = Enum.TryParse(overriddenConsistencyLevelString, out consistencyLevel);
            }

            _ = Enum.TryParse(defaultConsistencyLevelString, out ConsistencyLevel defaultConsistencyLevel);
            mockServiceConfigReader.Setup(x => x.DefaultConsistencyLevel).Returns(defaultConsistencyLevel);
            mockServiceConfigReader.Setup(x => x.TryGetConsistencyLevel(It.IsAny<DocumentServiceRequest>(), out consistencyLevel))
                .Returns(isConsistencyLevelOverridden);

            ReplicationPolicy replicationPolicy = new ReplicationPolicy();
            replicationPolicy.MaxReplicaSetSize = 4;
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            // Use QuorumMet transport mock — both paths succeed, but global strong requires more barrier reads
            var mockTransportClient = GetMockTransportClientForGlobalStrongReads(addressInformation, ReadQuorumResultKind.QuorumMet);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);

            // QuorumReader will extract IServiceConfigurationReaderExtension via `as` cast
            QuorumReader reader = new QuorumReader(
                mockTransportClient,
                addressSelector,
                storeReader,
                mockServiceConfigReader.Object,
                mockAuthorizationTokenProvider.Object);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.BoundedStaleness.ToString();
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();
            entity.RequestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();

            // OriginalRequestConsistencyLevel = null so condition #3 doesn't block
            entity.RequestContext.OriginalRequestConsistencyLevel = null;

            StoreResponse result = reader.ReadStrongAsync(entity, 2, ReadMode.Strong).Result;
            Assert.IsTrue(result.LSN == 100);

            if (expectedGlobalStrongRead)
            {
                // Global strong read: globalCommittedLSN should be 90 (from first quorum-met response)
                string globalCommittedLSN;
                result.TryGetHeaderValue(WFConstants.BackendHeaders.GlobalCommittedLSN, out globalCommittedLSN);
                long nGlobalCommittedLSN = long.Parse(globalCommittedLSN, CultureInfo.InvariantCulture);
                Assert.IsTrue(nGlobalCommittedLSN >= 0, "Global strong read should return a valid GlobalCommittedLSN");
            }

            // TryGetConsistencyLevel is always called when the extension is non-null
            mockServiceConfigReader.Verify(
                x => x.TryGetConsistencyLevel(It.IsAny<DocumentServiceRequest>(), out It.Ref<ConsistencyLevel>.IsAny),
                Times.AtLeastOnce());
        }

        [TestMethod]
        [Owner("swvya")]
        public void ShouldPerformWriteBarrierForGlobalStrong_ReturnsFalse_WhenSkipHeaderIsSet()
        {
            // Arrange
            var storeResult = StoreResult.CreateForTesting(new StoreResponse
            {
                Status = 200,
                Headers = new StoreResponseNameValueCollection
                {
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                }
            }).Target;

            var request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);
            request.Headers[HttpConstants.HttpHeaders.SkipSettingStrongConsistencyHeaderForWrites] = "true";

            AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade();
            var mockAddressCache = GetMockAddressCache(addressInformation);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            var mockTransportClient = GetMockTransportClientForGlobalStrongWrites(addressInformation, 0, false, false, false);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);

            // Act
            bool result = consistencyWriter.ShouldPerformWriteBarrierForGlobalStrong(storeResult, request);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Owner("swvya")]
        public void ShouldPerformWriteBarrierForGlobalStrong_ReturnsTrue_WhenAllConditionsMet()
        {
            // Arrange
            var storeResult = StoreResult.CreateForTesting(new StoreResponse
            {
                Status = 200,
                Headers = new StoreResponseNameValueCollection
        {
            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
        }
            }, numberOfReadRegions: 2).Target;

            var request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);

            AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade();
            var mockAddressCache = GetMockAddressCache(addressInformation);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            var mockTransportClient = GetMockTransportClientForGlobalStrongWrites(addressInformation, 0, false, false, false);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);


            // Act
            bool result = consistencyWriter.ShouldPerformWriteBarrierForGlobalStrong(storeResult, request);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [Owner("swvya")]
        public void ShouldPerformWriteBarrierForGlobalStrong_ReturnsFalse_WhenNotStrongConsistency()
        {
            // Arrange
            var storeResult = StoreResult.CreateForTesting(new StoreResponse
            {
                Status = 200,
                Headers = new StoreResponseNameValueCollection
        {
            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
        }
            }).Target;

            var request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);

            AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade();
            var mockAddressCache = GetMockAddressCache(addressInformation);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            var mockTransportClient = GetMockTransportClientForGlobalStrongWrites(addressInformation, 0, false, false, false);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);

            // Act
            bool result = consistencyWriter.ShouldPerformWriteBarrierForGlobalStrong(storeResult, request);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public async Task DisposeUnusedStoreResultsOnStoreReaderAsync()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            var requestContext = new DocumentServiceRequestContext();
            requestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary:  true,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new HashSet<TransportAddressUri>(addressInformation.Select(x => new TransportAddressUri(new Uri(x.PhysicalUri))));

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new PartitionAddressInformation(addressInformation)));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);

            TransportException transportException = new TransportException(TransportErrorCode.ReceiveFailed,
                                                                            null,
                                                                            Guid.NewGuid(),
                                                                            new Uri(addressInformation[0].PhysicalUri),
                                                                            "Source Description",
                                                                            true,
                                                                            true);

            StoreResponse storeResponse = new()
            {
                ResponseBody = new TestMemoryStream(),
                Headers = new DictionaryNameValueCollection(new NameValueCollection()
                    {
                        { WFConstants.BackendHeaders.LSN, "100"},
                        { WFConstants.BackendHeaders.ActivityId, $"ACTIVITYID" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                    })
            };

            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(
            It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
            entity))
            .ReturnsAsync(storeResponse);

            StoreReader storeReader = new StoreReader(
                        mockTransportClient.Object,
                        new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                        new AddressEnumerator(),
                        sessionContainer,
                        false);

            IList<ReferenceCountedDisposable<StoreResult>> results = await storeReader.ReadMultipleReplicaAsync(entity, true, 1, true, false, ReadMode.Any);
            Assert.AreEqual(results.Count, 1, "Unexpected result count");
            Assert.IsFalse(((TestMemoryStream)results.Single().Target.ToResponse().ResponseBody).IsDisposed, "Stream was disposed");
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public async Task UnusedResponseDisposalWithBoundedStanelessTestAsync()
        {
            // create a real document service request (with auth token level = god)
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            var requestContext = new DocumentServiceRequestContext();
            requestContext.RequestChargeTracker = new RequestChargeTracker();
            entity.RequestContext = requestContext;

            // set a dummy resource id on the request.
            entity.ResourceId = "1-MxAPlgMgA=";

            // set consistency level on the request to Bounded Staleness
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.BoundedStaleness.ToString();

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            // when the store reader throws Invalid Partition exception, the higher layer should
            // clear this target identity.
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = GetMockAddressInformationDuringUpgrade(count: 4);
            var mockAddressCache = GetMockAddressCache(addressInformation);

            // validate that the mock works
            PartitionAddressInformation partitionAddressInformation = mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken()).Result;
            var addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/).Result.Uri;

            // check if the address return from Address Selector matches the original address info
            Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

            // Response count < readQuorumValue
            {
                int storeResponseCreatedCount = 0;
                int storeResponseDisposedCount = 0;
                QuorumReader reader = PrepareQuorumReader(
                    addressSelector,
                    addressInformation,
                    responsesFailedCount: 1,
                    lsn: 100,
                    globalLsn: 101,
                    createAction: () => storeResponseCreatedCount++,
                    disposeAction: () => storeResponseDisposedCount++);
                entity.RequestContext.OriginalRequestConsistencyLevel = ConsistencyLevel.Strong;
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
                await Assert.ThrowsExceptionAsync<GoneException>(async () => await reader.ReadStrongAsync(entity, readQuorumValue: 3, ReadMode.Strong));

                // 2 read replica in a first call and 1 write relica on a follow up call
                Assert.IsTrue(storeResponseCreatedCount > 0, "Test is inconsistent");
                Assert.AreEqual(storeResponseCreatedCount, storeResponseDisposedCount, "Unexpected number of disposed streams");
            }

            // Response count == readQuorumValue
            {
                int storeResponseCreatedCount = 0;
                int storeResponseDisposedCount = 0;
                QuorumReader reader = PrepareQuorumReader(
                    addressSelector,
                    addressInformation,
                    responsesFailedCount: 0,
                    lsn: 100,
                    globalLsn: 101,
                    createAction: () => storeResponseCreatedCount++,
                    disposeAction: () => storeResponseDisposedCount++);
                entity.RequestContext.OriginalRequestConsistencyLevel = ConsistencyLevel.Strong;
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
                StoreResponse result = await reader.ReadStrongAsync(entity, readQuorumValue: 2, ReadMode.Strong);
                Assert.IsFalse(((TestMemoryStream)result.ResponseBody).IsDisposed, "Returned response could not be disposed");

                // Second response must be disposed. Subtract 1 for returned storeResponse
                Assert.IsTrue(storeResponseCreatedCount > 0, "Test is inconsistent");
                Assert.AreEqual(storeResponseCreatedCount - 1, storeResponseDisposedCount, "Unexpected number of disposed streams");
            }

            // Response count == readQuorumValue but commited lsn < lsn
            {
                int storeResponseCreatedCount = 0;
                int storeResponseDisposedCount = 0;
                QuorumReader reader = PrepareQuorumReader(
                    addressSelector,
                    addressInformation,
                    responsesFailedCount: 0,
                    lsn: 100,
                    globalLsn: 99,
                    createAction: () => storeResponseCreatedCount++,
                    disposeAction: () => storeResponseDisposedCount++);
                entity.RequestContext.OriginalRequestConsistencyLevel = ConsistencyLevel.Strong;
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();

                SourceSwitch initialSwitch = DefaultTrace.TraceSource.Switch;
                TraceEventListener traceEventListener = new TraceEventListener();
                try
                {
                    DefaultTrace.TraceSource.Switch = new SourceSwitch("SourceSwitch") { Level = SourceLevels.Warning };
                    DefaultTrace.TraceSource.Listeners.Add(traceEventListener);

                    await Assert.ThrowsExceptionAsync<GoneException>(async () => await reader.ReadStrongAsync(entity, readQuorumValue: 2, ReadMode.Strong));
                }
                finally
                {
                    DefaultTrace.TraceSource.Switch = initialSwitch;
                    DefaultTrace.TraceSource.Listeners.Remove(traceEventListener);
                }

                // Validate warning trace
                (string, object[]) quorumSelectedLsnWarning = default;
                while (traceEventListener.Warnings.TryDequeue(out (string, object[]) warning))
                {
                    if (warning.Item1.StartsWith("QuorumSelected: Could not converge on the LSN"))
                    {
                        quorumSelectedLsnWarning = warning;
                    }
                }
                Assert.IsNotNull(quorumSelectedLsnWarning.Item1, "Reader failed to report LSN warning");
                Assert.IsNotNull(quorumSelectedLsnWarning.Item2[3], "Unexpected to miss StoreResult report");

                // Second response must be disposed
                Assert.IsTrue(storeResponseCreatedCount > 0, "Test is inconsistent");
                Assert.AreEqual(storeResponseCreatedCount - 1, storeResponseDisposedCount, "Unexpected number of disposed streams");

                // Not sure if this is really needed....
                Assert.IsFalse(((TestMemoryStream)entity.RequestContext?.QuorumSelectedStoreResponse.Target.ToResponse().ResponseBody)?.IsDisposed ?? null, "Request context holds an active stream...");
            }
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public void DocumentServiceRequestContextTest()
        {
            // Sanity check
            TestMemoryStream testMemoryStream = new();
            testMemoryStream.Dispose();
            Assert.IsTrue(testMemoryStream.IsDisposed, "Unexpected IsDisposed on sanity check");

            DocumentServiceRequestContext context = new();
            StoreResponse storeResponse = new()
            {
                ResponseBody = new TestMemoryStream()
            };

            // Sanity Check
            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(storeResponse);
            context.UpdateQuorumSelectedStoreResponse(storeResult);
            Assert.IsFalse(((TestMemoryStream)storeResponse.ResponseBody).IsDisposed, "Unexpected SkipResponseBodyDispose value");

            // Assigned store result will get disposed after new store result is assigned
            context.UpdateQuorumSelectedStoreResponse(null);
            Assert.IsTrue(((TestMemoryStream)storeResponse.ResponseBody).IsDisposed, "Unexpected IsDisposed on StoreResponse de-references by DocumentServiceRequestContext");
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public void TestQuorumSelectedStoreResponseAssignmentUsedInTest()
        {
            StoreResponse originalStoreResponse = new()
            {
                ResponseBody = new TestMemoryStream()
            };
            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(originalStoreResponse);
            Assert.IsFalse(((TestMemoryStream)originalStoreResponse.ResponseBody).IsDisposed, "Incorrect initial data");

            DocumentServiceRequestContext context = new();
            context.UpdateQuorumSelectedStoreResponse(storeResult);
            Assert.IsFalse(((TestMemoryStream)originalStoreResponse.ResponseBody).IsDisposed, "Unexpected to get stream disposed after initial QuorumSelectedStoreResponse assignment");

            // Confirm that assignment to the same storeResult does not result in disposal 
            context.UpdateQuorumSelectedStoreResponse(storeResult);
            Assert.IsFalse(((TestMemoryStream)originalStoreResponse.ResponseBody).IsDisposed, "Unexpected to get stream disposed after assigning QuorumSelectedStoreResponse to the same value");

            // Assign to a new StoreResult. Old StoreResult will get disposed
            StoreResponse newStoreResponse = new()
            {
                ResponseBody = new TestMemoryStream()
            };
            ReferenceCountedDisposable<StoreResult> newStoreResult = StoreResult.CreateForTesting(newStoreResponse);
            Assert.IsFalse(((TestMemoryStream)newStoreResponse.ResponseBody).IsDisposed, "Incorrect initial data");
            context.UpdateQuorumSelectedStoreResponse(newStoreResult);

            Assert.IsTrue(((TestMemoryStream)originalStoreResponse.ResponseBody).IsDisposed, "QuorumSelectedStoreResponse set to a new StoreResult causes an old StoreResult to be disposed");
            Assert.IsFalse(((TestMemoryStream)newStoreResponse.ResponseBody).IsDisposed, "Unexpected to get stream disposed after assigning it to QuorumSelectedStoreResponse");
        }

        /// <summary>
        /// Test to validate that, when replica validation is enabled, the store reader considers the replicas
        /// in the order of Connected/ Unknown > UnhealthyPending > Unhealthy for reading.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, 4)]
        [DataRow(false, 3)]
        public async Task ReadMultipleReplicaAsync_WithPrimaryIncludedOrExcludedAndMultipleReplicaStatuses_ShouldPickHealthyReplicaForReads(
            bool includePrimary,
            int expectedTotalUriCount)
        {
            // Arrange.
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // Set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            DocumentServiceRequestContext requestContext = new()
            {
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                RequestChargeTracker = new RequestChargeTracker()
            };

            entity.RequestContext = requestContext;
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(
                timeOut: new TimeSpan(2, 2, 2));

            AddressInformation[] addressInformations = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0000p",
                    isPrimary: true,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),

                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0001s",
                    isPrimary: false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),

                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0002s",
                    isPrimary: false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false),

                new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/0003s",
                    isPrimary: false,
                    protocol:  Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false)
            };

            HashSet<TransportAddressUri> transportAddressUriHashset = new(
                addressInformations.Select(
                    x => new TransportAddressUri(
                        addressUri: new Uri(x.PhysicalUri))));

            int healthyIndex = 2, unknownIndex = 0, unhealthyIndex = 1, unhealthyPendingIndex = 3, currentIndex = 0;
            PartitionAddressInformation partitionAddressInformation = new(addressInformations);
            foreach (TransportAddressUri address in partitionAddressInformation.Get(Protocol.Tcp)?.ReplicaTransportAddressUris)
            {
                if (currentIndex == healthyIndex)
                {
                    address.SetConnected();
                }
                if (currentIndex == unhealthyIndex)
                {
                    address.SetUnhealthy();
                }
                if (currentIndex == unhealthyPendingIndex)
                {
                    address.SetUnhealthy();
                    address.SetRefreshedIfUnhealthy();
                }
                currentIndex++;
            }

            Mock<IAddressResolverExtension> mockAddressResolver = new Mock<IAddressResolverExtension>(MockBehavior.Strict);
            mockAddressResolver.Setup(x => x.ResolveAsync(
                entity,
                false,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(partitionAddressInformation));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            // create mock store response object
            StoreResponse mockStoreResponse = new()
            {
                // set lsn and activityid on the store response.
                Headers = new DictionaryNameValueCollection(
                new NameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "50"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                })
            };

            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransportClient.Setup(x => x.InvokeResourceOperationAsync(
                It.Is<TransportAddressUri>(x => transportAddressUriHashset.Contains(x)),
                entity))
                .ReturnsAsync(mockStoreResponse);

            StoreReader storeReader = new(
                        transportClient: mockTransportClient.Object,
                        addressSelector: new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                        addressEnumerator: new AddressEnumerator(),
                        sessionContainer: sessionContainer,
                        enableReplicaValidation: true);

            // Act.
            IList<ReferenceCountedDisposable<StoreResult>> storeResults = await storeReader.ReadMultipleReplicaAsync(
                entity: entity,
                includePrimary: includePrimary,
                replicaCountToRead: expectedTotalUriCount,
                requiresValidLsn: true,
                useSessionToken: false,
                ReadMode.Any);

            // Assert.
            Assert.IsNotNull(storeResults);
            Assert.AreEqual(
                expected: expectedTotalUriCount,
                actual: storeResults.Count);

            Assert.IsTrue(
                condition: partitionAddressInformation
                                .Get(Protocol.Tcp)
                                .ReplicaTransportAddressUris[healthyIndex]
                                .ToString()
                                .Equals(storeResults[0]
                                .Target
                                .StorePhysicalAddress
                                .OriginalString,
                                StringComparison.Ordinal) ||
                            partitionAddressInformation
                                .Get(Protocol.Tcp)
                                .ReplicaTransportAddressUris[unknownIndex]
                                .ToString()
                                .Equals(storeResults[0]
                                .Target
                                .StorePhysicalAddress
                                .OriginalString,
                                StringComparison.Ordinal),
                message: "Validate that either the healthy or unknown replicas are at the begining of the returned replica uris.");

            Assert.AreEqual(
                expected: partitionAddressInformation
                                .Get(Protocol.Tcp)
                                .ReplicaTransportAddressUris[unhealthyPendingIndex]
                                .ToString(),
                actual: storeResults[includePrimary ? 2 : 1].Target.StorePhysicalAddress.OriginalString,
                message: "Validate that the unhealthy pending replicas are followed by the healthy or unknown replicas in the returned replica uris.");

            Assert.AreEqual(
                expected: partitionAddressInformation
                                .Get(Protocol.Tcp)
                                .ReplicaTransportAddressUris[unhealthyIndex]
                                .ToString(),
                actual: storeResults[includePrimary ? 3 : 2].Target.StorePhysicalAddress.OriginalString,
                message: "Validate that the unhealthy replicas are towards the end of the returned replica uris.");
        }

        private QuorumReader PrepareQuorumReader(
            AddressSelector addressSelector,
            AddressInformation[] addressInformation,
            int responsesFailedCount,
            int lsn,
            int globalLsn,
            Action createAction,
            Action disposeAction)
        {
            // create objects for all the dependencies of the StoreReader
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            for (int i = 0; i < addressInformation.Length; i++)
            {
                if (i < addressInformation.Length - responsesFailedCount)
                {
                    mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new Uri(addressInformation[i].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(() => new StoreResponse()
                        {
                            ResponseBody = new TestMemoryStream()
                            {
                                DisposeAction = disposeAction
                            },
                            Headers = new DictionaryNameValueCollection(new NameValueCollection()
                            {
                                { WFConstants.BackendHeaders.LSN, lsn.ToString()},
                                { WFConstants.BackendHeaders.ActivityId, $"ACTIVITYID1_{i}" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, globalLsn.ToString() },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                            })
                        })
                        .Callback(() => createAction());
                    mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(() => new StoreResponse()
                        {
                            ResponseBody = new TestMemoryStream()
                            {
                                DisposeAction = disposeAction
                            },
                            Headers = new DictionaryNameValueCollection(new NameValueCollection()
                            {
                                { WFConstants.BackendHeaders.LSN, lsn.ToString()},
                                { WFConstants.BackendHeaders.ActivityId, $"ACTIVITYID1_{i}" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, globalLsn.ToString() },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                            })
                        })
                        .Callback(() => createAction());
                }
                else
                {
                    mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new Uri(addressInformation[i].PhysicalUri), It.IsAny<DocumentServiceRequest>()))
                        .Throws(new Exception());
                    mockTransportClient.Setup(
                        client => client.InvokeResourceOperationAsync(
                            new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .Throws(new Exception());
                }
            }

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
            StoreReader storeReader = new(
                mockTransportClient.Object,
                addressSelector,
                new AddressEnumerator(),
                sessionContainer,
                false);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            // setup max replica set size on the config reader
            ReplicationPolicy replicationPolicy = new();
            replicationPolicy.MaxReplicaSetSize = 4;
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

            return new(mockTransportClient.Object, addressSelector, storeReader, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object);
        }

        #region CRSS Scale-Up Discovery Tests

        /// <summary>
        /// Verifies that RefreshAddressesIfReplicaSetSizeChanged triggers a background
        /// refresh when CRSS exceeds both the resolved address count and the per-partition
        /// target replica set size (scale-up from reduced target detected).
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task RefreshAddressesIfReplicaSetSizeChanged_ScaleUp_TriggersBackgroundRefreshAsync()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                ResolvedPartitionTargetReplicaSetSize = 3,
                ResolvedReplicaAddressCountPerProtocol = 3
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            mockResolver.Setup(x => x.ResolveAsync(
                It.Is<DocumentServiceRequest>(d => !object.ReferenceEquals(entity, d)),
                true,
                It.IsAny<CancellationToken>())).Returns(() => Task.FromResult((PartitionAddressInformation)null));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);
            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: 4);

            Assert.IsTrue(entity.RequestContext.PerformedBackgroundAddressRefresh);
            await DirectStoreReaderTests.VerifyBackgroundRefreshHelpAsync(mockResolver);
        }

        /// <summary>
        /// Verifies that null TRSS (per-service target not active) gates the check and
        /// prevents any background refresh, even when CRSS exceeds cached count.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public void RefreshAddressesIfReplicaSetSizeChanged_NullTrss_NoRefresh()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                ResolvedPartitionTargetReplicaSetSize = null,
                ResolvedReplicaAddressCountPerProtocol = 3
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: 4);

            Assert.IsFalse(entity.RequestContext.PerformedBackgroundAddressRefresh);
            // Strict mock � no setup for ResolveAsync means it would throw if called.
        }

        /// <summary>
        /// Verifies that CRSS equal to cached address count does not trigger refresh
        /// (no scale-up detected).
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public void RefreshAddressesIfReplicaSetSizeChanged_CrssEqualsCached_NoRefresh()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                ResolvedPartitionTargetReplicaSetSize = 3,
                ResolvedReplicaAddressCountPerProtocol = 3
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: 3);

            Assert.IsFalse(entity.RequestContext.PerformedBackgroundAddressRefresh);
        }

        /// <summary>
        /// Verifies that CRSS equal to TRSS does not trigger refresh. This case
        /// (TRSS=4, cached=3) is handled by the existing suboptimal detection.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public void RefreshAddressesIfReplicaSetSizeChanged_CrssEqualsTrss_NoRefresh()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                ResolvedPartitionTargetReplicaSetSize = 4,
                ResolvedReplicaAddressCountPerProtocol = 3
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: 4);

            Assert.IsFalse(entity.RequestContext.PerformedBackgroundAddressRefresh);
        }

        /// <summary>
        /// Verifies that when PerformedBackgroundAddressRefresh flag is already set
        /// (e.g., by GoneException or prior CRSS trigger), the check is skipped entirely.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public void RefreshAddressesIfReplicaSetSizeChanged_FlagAlreadySet_Skips()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                ResolvedPartitionTargetReplicaSetSize = 3,
                ResolvedReplicaAddressCountPerProtocol = 3,
                PerformedBackgroundAddressRefresh = true
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: 4);

            // Flag stays true, no ResolveAsync call (strict mock would throw)
            Assert.IsTrue(entity.RequestContext.PerformedBackgroundAddressRefresh);
        }

        /// <summary>
        /// Verifies that negative CRSS (old backend without the header, default = -1)
        /// does not trigger refresh.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public void RefreshAddressesIfReplicaSetSizeChanged_NegativeCrss_NoRefresh()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                ResolvedPartitionTargetReplicaSetSize = 3,
                ResolvedReplicaAddressCountPerProtocol = 3
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: -1);

            Assert.IsFalse(entity.RequestContext.PerformedBackgroundAddressRefresh);
        }

        /// <summary>
        /// Verifies that ResolveAddressesAsync stashes PartitionTargetReplicaSetSize
        /// on RequestContext from the resolved PartitionAddressInformation.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task ResolveAddressesAsync_StashesTrssOnRequestContextAsync()
        {
            AddressInformation[] addresses = DirectStoreReaderTests.CreateAddressInformation(3);
            PartitionAddressInformation pai = new PartitionAddressInformation(addresses, false, partitionTargetReplicaSetSize: 3);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics()
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pai);

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);
            await addressSelector.ResolveAddressesAsync(entity, false);

            Assert.AreEqual(3, entity.RequestContext.ResolvedPartitionTargetReplicaSetSize);
        }

        /// <summary>
        /// Verifies that ResolveAddressesAsync stashes per-protocol address count
        /// on RequestContext.ResolvedReplicaAddressCountPerProtocol.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task ResolveAddressesAsync_StashesAddressCountOnRequestContextAsync()
        {
            AddressInformation[] addresses = DirectStoreReaderTests.CreateAddressInformation(3);
            PartitionAddressInformation pai = new PartitionAddressInformation(addresses, false, partitionTargetReplicaSetSize: 3);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics()
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pai);

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);
            await addressSelector.ResolveAddressesAsync(entity, false);

            Assert.AreEqual(3, entity.RequestContext.ResolvedReplicaAddressCountPerProtocol);
        }

        /// <summary>
        /// Verifies that ResolveAddressesAsync stashes null TRSS when
        /// PartitionAddressInformation does not have per-service target set.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task ResolveAddressesAsync_NullTrss_StashesNullAsync()
        {
            AddressInformation[] addresses = DirectStoreReaderTests.CreateAddressInformation(3);
            PartitionAddressInformation pai = new PartitionAddressInformation(addresses, false, partitionTargetReplicaSetSize: null);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics()
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pai);

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);
            await addressSelector.ResolveAddressesAsync(entity, false);

            Assert.IsNull(entity.RequestContext.ResolvedPartitionTargetReplicaSetSize);
        }

        /// <summary>
        /// Verifies that when ResolvedReplicaAddressCountPerProtocol is 0 (default, before ResolveAddressesAsync
        /// is called) and TRSS is null, the HasValue gate prevents any false refresh trigger.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public void RefreshAddressesIfReplicaSetSizeChanged_DefaultResolvedReplicaAddressCountPerProtocol_NullTrss_NoRefresh()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            // RequestContext with default values � ResolvedReplicaAddressCountPerProtocol=0, TRSS=null
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics()
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>(MockBehavior.Strict);
            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            // CRSS=4 > ResolvedReplicaAddressCountPerProtocol=0 would be true, but TRSS null gate prevents trigger
            addressSelector.RefreshAddressesIfReplicaSetSizeChanged(entity, currentReplicaSetSizeFromResponse: 4);

            Assert.IsFalse(entity.RequestContext.PerformedBackgroundAddressRefresh);
        }

        private static AddressInformation[] CreateAddressInformation(int count)
        {
            AddressInformation[] addresses = new AddressInformation[count];
            for (int i = 0; i < count; i++)
            {
                addresses[i] = new AddressInformation(
                    physicalUri: $"rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/{i:D4}{(i == 0 ? "p" : "s")}",
                    isPrimary: i == 0,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            return addresses;
        }

        /// <summary>
        /// Verifies that when backend response carries CRSS > cached address
        /// count, the StoreReader stashes the max CRSS on RequestContext
        /// for centralized scale-up detection in ReplicatedResourceClient.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task ReadMultipleReplica_CrssExceedsCached_StashesMaxCrssAsync()
        {
            AddressInformation[] addresses = DirectStoreReaderTests.CreateAddressInformation(3);
            PartitionAddressInformation pai = new PartitionAddressInformation(addresses, false, partitionTargetReplicaSetSize: 3);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver.Setup(r => r.ResolveAsync(
                It.IsAny<DocumentServiceRequest>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pai);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport.Setup(t => t.InvokeResourceOperationAsync(
                It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 200,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.ActivityId, "activityId" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, "4" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" }
                    })
                });

            StoreReader storeReader = new StoreReader(
                mockTransport.Object,
                new AddressSelector(mockResolver.Object, Protocol.Tcp),
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);

            await storeReader.ReadMultipleReplicaAsync(
                entity, includePrimary: true, replicaCountToRead: 1,
                requiresValidLsn: false, useSessionToken: false,
                readMode: ReadMode.Any);

            Assert.AreEqual(4, entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse);
        }

        /// <summary>
        /// Verifies that when CRSS equals cached count, max CRSS stash reflects
        /// the response value but does not exceed cached count.
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task ReadMultipleReplica_CrssEqualsCached_StashesButNoExceedAsync()
        {
            AddressInformation[] addresses = DirectStoreReaderTests.CreateAddressInformation(3);
            PartitionAddressInformation pai = new PartitionAddressInformation(addresses, false, partitionTargetReplicaSetSize: 3);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver.Setup(r => r.ResolveAsync(
                It.IsAny<DocumentServiceRequest>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pai);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport.Setup(t => t.InvokeResourceOperationAsync(
                It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 200,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.ActivityId, "activityId" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" }
                    })
                });

            StoreReader storeReader = new StoreReader(
                mockTransport.Object,
                new AddressSelector(mockResolver.Object, Protocol.Tcp),
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);

            await storeReader.ReadMultipleReplicaAsync(
                entity, includePrimary: true, replicaCountToRead: 1,
                requiresValidLsn: false, useSessionToken: false,
                readMode: ReadMode.Any);

            Assert.AreEqual(3, entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse);
        }

        /// <summary>
        /// Verifies that when 3 replicas all return CRSS=4, the max CRSS
        /// is stashed as 4 on RequestContext (single value, not accumulated).
        /// </summary>
        [TestMethod]
        [Owner("cosmosdbrg")]
        public async Task ReadMultipleReplica_ThreeReplicas_StashesMaxCrssAsync()
        {
            AddressInformation[] addresses = DirectStoreReaderTests.CreateAddressInformation(3);
            PartitionAddressInformation pai = new PartitionAddressInformation(addresses, false, partitionTargetReplicaSetSize: 3);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver.Setup(r => r.ResolveAsync(
                It.IsAny<DocumentServiceRequest>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pai);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport.Setup(t => t.InvokeResourceOperationAsync(
                It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 200,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.ActivityId, "activityId" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, "4" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" }
                    })
                });

            StoreReader storeReader = new StoreReader(
                mockTransport.Object,
                new AddressSelector(mockResolver.Object, Protocol.Tcp),
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);

            await storeReader.ReadMultipleReplicaAsync(
                entity, includePrimary: true, replicaCountToRead: 3,
                requiresValidLsn: false, useSessionToken: false,
                readMode: ReadMode.Any, forceReadAll: true);

            Assert.AreEqual(4, entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse,
                "Max CRSS should be 4 regardless of how many replicas returned it.");
        }

        #endregion

        internal sealed class TestMemoryStream : MemoryStream
        {
            public Action DisposeAction { get; set; }

            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                this.IsDisposed = true;
                this.DisposeAction?.Invoke();
            }
        }

        internal sealed class TraceEventListener : TraceListener
        {
            public ConcurrentQueue<(string, object[])> Warnings { get; } = new ConcurrentQueue<(string, object[])>();

            public ConcurrentQueue<(string, object[])> Errors { get; } = new ConcurrentQueue<(string, object[])>();

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
            {
                switch (eventType)
                {
                    case TraceEventType.Warning:
                        this.Warnings.Enqueue((format, args));
                        break;
                    case TraceEventType.Error:
                        this.Errors.Enqueue((format, args));
                        break;
                }
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
            {
                this.TraceEvent(eventCache, source, eventType, id, message, null);
            }

            public override void Write(string message)
            {
            }

            public override void WriteLine(string message)
            {
            }
        }
    }
}