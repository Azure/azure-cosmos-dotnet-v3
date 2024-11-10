//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="StoreReader"/>
    /// </summary>
    [TestClass]
    public class StoreReaderTest
    {
        /// <summary>
        /// Tests for <see cref="IAddressResolver"/>
        /// </summary>
        [TestMethod]
        public void AddressCacheMockTest()
        {
            // create a real document service request
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // setup mocks for address information
            AddressInformation[] addressInformation = new AddressInformation[3];
            for (int i = 0; i < 3; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "http://replica-" + i.ToString("G", CultureInfo.CurrentCulture),
                    isPrimary: false,
                    protocol: default,
                    isPublic: false);
            }

            // Address Selector is an internal sealed class that can't be mocked, but its dependency
            // AddressCache can be mocked.
            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    false /*forceRefresh*/,
                    new CancellationToken()))
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            // validate that the mock works
            PartitionAddressInformation addressInfo = mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken()).Result;
            Assert.IsTrue(addressInfo.AllAddresses[0] == addressInformation[0]);
        }

        /// <summary>
        /// Tests for <see cref="TransportClient"/>
        /// </summary>
        [TestMethod]
        public void TransportClientMockTest()
        {
            // create a real document service request
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // setup mocks for address information
            AddressInformation[] addressInformation = new AddressInformation[3];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i < 3; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: false,
                    protocol: default,
                    isPublic: false);
            }

            // create objects for all the dependencies of the StoreReader
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // create mock store response object
            StoreResponse mockStoreResponse = new StoreResponse
            {

                // set lsn and activityid on the store response.
                Headers = new Documents.Collections.RequestNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "50"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                }
            };

            // setup mock transport client
            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse);

            TransportClient mockTransportClientObject = mockTransportClient.Object;
            // get response from mock object
            StoreResponse response = mockTransportClientObject.InvokeResourceOperationAsync(new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), entity).Result;

            // validate that the LSN matches
            Assert.IsTrue(response.LSN == 50);

            response.TryGetHeaderValue(WFConstants.BackendHeaders.ActivityId, out string activityId);

            // validate that the ActivityId Matches
            Assert.IsTrue(activityId == "ACTIVITYID1_1");
        }

        private TransportClient GetMockTransportClientDuringUpgrade(AddressInformation[] addressInformation)
        {
            // create objects for all the dependencies of the StoreReader
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // create mock store response object
            StoreResponse mockStoreResponseFast = new StoreResponse();
            StoreResponse mockStoreResponseSlow = new StoreResponse();

            // set lsn and activityid on the store response.
            mockStoreResponseFast.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "50"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                };

            // set lsn and activityid on the store response.
            mockStoreResponseSlow.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "30"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                };

            // setup mock transport client for the first replica
            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponseFast);

            // setup mock transport client with a sequence of outputs
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

            Queue<StoreResponse> queueOfResponses = new Queue<StoreResponse>();

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
            mockStoreResponse1.Headers = new StoreResponseNameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            };

            mockStoreResponse2.Headers = new StoreResponseNameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "90"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_2" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            };

            mockStoreResponse3.Headers = new StoreResponseNameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "92"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            };

            mockStoreResponse4.Headers = new StoreResponseNameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "92" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
            };

            mockStoreResponse5.Headers = new StoreResponseNameValueCollection()
            {
                { WFConstants.BackendHeaders.LSN, "100"},
                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "1" },
                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
            };

            if (result == ReadQuorumResultKind.QuorumMet)
            {
                // setup mock transport client for the first replica
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse5);

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
                        new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse2);

                // setup mock transport client with a sequence of outputs
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new TransportAddressUri(new Uri(addressInformation[1].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse1);

                // setup mock transport client with a sequence of outputs
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
                        new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .ReturnsAsync(mockStoreResponse5);

                mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new TransportAddressUri(new Uri(addressInformation[1].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse5);

                mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    new TransportAddressUri(new Uri(addressInformation[2].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                    .Throws(new GoneException("test"));
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
            mockStoreResponse1.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "100"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse2.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "100"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_2" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse3.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "103"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse4.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "103"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "103" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse5.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "106"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "103" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };
            StoreResponse finalResponse = null;
            if (undershootGlobalCommittedLsnDuringBarrier)
            {
                finalResponse = mockStoreResponse1;
            }
            else
            {
                if (overshootLsnDuringBarrier)
                {
                    finalResponse = overshootGlobalCommittedLsnDuringBarrier ? mockStoreResponse5 : mockStoreResponse3;
                }
                else
                {
                    finalResponse = overshootGlobalCommittedLsnDuringBarrier ? mockStoreResponse4 : mockStoreResponse2;
                }
            }

            for (int i = 0; i < addressInformation.Length; i++)
            {
                if (i == indexOfCaughtUpReplica)
                {
                    mockTransportClient.SetupSequence(
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
        private AddressInformation[] GetMockAddressInformationDuringUpgrade()
        {
            // setup mocks for address information
            AddressInformation[] addressInformation = new AddressInformation[3];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i <= 2; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0,
                    protocol: Documents.Client.Protocol.Tcp,
                    isPublic: true);
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
        public void StoreReaderBarrierTest()
        {
            // create a real document service request
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext
            {
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                RequestChargeTracker = new RequestChargeTracker()
            };
            entity.RequestContext = requestContext;

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            // when the store reader throws Invalid Partition exception, the higher layer should
            // clear this target identity.
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);

            // validate that the mock works
            PartitionAddressInformation partitionAddressInformation = mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken()).Result;
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/).Result.Uri;

            // check if the address return from Address Selector matches the original address info
            Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

            // get mock transport client that returns a sequence of responses to simulate upgrade
            TransportClient mockTransportClient = this.GetMockTransportClientDuringUpgrade(addressInformation);

            // get response from mock object
            StoreResponse response = mockTransportClient.InvokeResourceOperationAsync(new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), entity).Result;

            // validate that the LSN matches
            Assert.IsTrue(response.LSN == 50);

            response.TryGetHeaderValue(WFConstants.BackendHeaders.ActivityId, out string activityId);

            // validate that the ActivityId Matches
            Assert.IsTrue(activityId == "ACTIVITYID1_1");

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
            StoreReader storeReader =
                new StoreReader(mockTransportClient,
                    addressSelector,
                    new AddressEnumerator(),
                    sessionContainer,
                    enableReplicaValidation: false);

            // reads always go to read quorum (2) replicas
            int replicaCountToRead = 2;

            IList<ReferenceCountedDisposable<StoreResult>> result = storeReader.ReadMultipleReplicaAsync(
                    entity,
                    false /*includePrimary*/,
                    replicaCountToRead,
                    true /*requiresValidLSN*/ ,
                    false /*useSessionToken*/,
                    ReadMode.Strong).Result;

            // make sure we got 2 responses from the store reader
            Assert.IsTrue(result.Count == 2);
        }

        /// <summary>
        /// test consistency writer for global strong
        /// </summary>
        [TestMethod]
        public void GlobalStrongConsistentWriteMockTest()
        {
            // create a real document service request (with auth token level = god)
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker()
            };
            entity.RequestContext = requestContext;

            // set a dummy resource id on the request.
            entity.ResourceId = "1-MxAPlgMgA=";

            // set consistency level on the request to Bounded Staleness
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            // when the store reader throws Invalid Partition exception, the higher layer should
            // clear this target identity.
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);

            // validate that the mock works
            PartitionAddressInformation partitionAddressInformation = mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken()).Result;
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/).Result.Uri;

            // check if the address return from Address Selector matches the original address info
            Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

            // create a real session container - we don't need session for this test anyway
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            for (int i = 0; i < addressInformation.Length; i++)
            {
                TransportClient mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, false, false);
                StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);
                ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                StoreResponse response = consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false).Result;
                Assert.AreEqual(100, response.LSN);

                //globalCommittedLsn never catches up in this case
                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, true, false, false);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                try
                {
                    response = consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false).Result;
                    Assert.Fail();
                }
                catch (Exception)
                {
                }

                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, true, false);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false).Result;
                Assert.AreEqual(100, response.LSN);

                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, true, true);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false).Result;
                Assert.AreEqual(100, response.LSN);

                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, false, true);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false).Result;
                Assert.AreEqual(100, response.LSN);
            }
        }

        /// <summary>
        /// Mocking Consistency
        /// </summary>
        [TestMethod]
        public void GlobalStrongConsistencyMockTest()
        {
            // create a real document service request (with auth token level = god)
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker()
            };
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

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);

            // validate that the mock works
            PartitionAddressInformation partitionAddressInformation = mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken()).Result;
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/).Result.Uri;

            // check if the address return from Address Selector matches the original address info
            Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

            // Quorum Met scenario
            {
                // get mock transport client that returns a sequence of responses to simulate upgrade
                TransportClient mockTransportClient = this.GetMockTransportClientForGlobalStrongReads(addressInformation, ReadQuorumResultKind.QuorumMet);

                // create a real session container - we don't need session for this test anyway
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);

                // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
                StoreReader storeReader =
                    new StoreReader(mockTransportClient,
                        addressSelector,
                        new AddressEnumerator(),
                        sessionContainer,
                        false);

                Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
                mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                    It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(0));

                // setup max replica set size on the config reader
                ReplicationPolicy replicationPolicy = new ReplicationPolicy
                {
                    MaxReplicaSetSize = 4
                };
                Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
                mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

                QuorumReader reader =
                    new QuorumReader(mockTransportClient, addressSelector, storeReader, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object);

                entity.RequestContext.OriginalRequestConsistencyLevel = Documents.ConsistencyLevel.Strong;
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();

                StoreResponse result = reader.ReadStrongAsync(entity, 2, ReadMode.Strong).Result;
                Assert.IsTrue(result.LSN == 100);

                result.TryGetHeaderValue(WFConstants.BackendHeaders.GlobalCommittedLSN, out string globalCommitedLSN);

                long nGlobalCommitedLSN = long.Parse(globalCommitedLSN, CultureInfo.InvariantCulture);
                Assert.IsTrue(nGlobalCommitedLSN == 90);
            }

            // Quorum Selected scenario
            {
                // get mock transport client that returns a sequence of responses to simulate upgrade
                TransportClient mockTransportClient = this.GetMockTransportClientForGlobalStrongReads(addressInformation, ReadQuorumResultKind.QuorumSelected);

                // create a real session container - we don't need session for this test anyway
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);

                // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
                StoreReader storeReader =
                    new StoreReader(mockTransportClient,
                        addressSelector,
                        new AddressEnumerator(),
                        sessionContainer,
                        false);

                Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
                mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                    It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(0));

                // setup max replica set size on the config reader
                ReplicationPolicy replicationPolicy = new ReplicationPolicy
                {
                    MaxReplicaSetSize = 4
                };
                Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
                mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

                QuorumReader reader =
                    new QuorumReader(mockTransportClient, addressSelector, storeReader, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object);

                entity.RequestContext.OriginalRequestConsistencyLevel = Documents.ConsistencyLevel.Strong;
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
                entity.RequestContext.QuorumSelectedLSN = -1;
                entity.RequestContext.GlobalCommittedSelectedLSN = -1;
                try
                {
                    StoreResponse result = reader.ReadStrongAsync(entity, 2, ReadMode.Strong).Result;
                    Assert.IsTrue(false);
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException is GoneException)
                    {
                        DefaultTrace.TraceInformation("Gone exception expected!");
                    }
                }

                Assert.IsTrue(entity.RequestContext.QuorumSelectedLSN == 100);
                Assert.IsTrue(entity.RequestContext.GlobalCommittedSelectedLSN == 100);
            }

            // Quorum not met scenario
            {
                // get mock transport client that returns a sequence of responses to simulate upgrade
                TransportClient mockTransportClient = this.GetMockTransportClientForGlobalStrongReads(addressInformation, ReadQuorumResultKind.QuorumNotSelected);

                // create a real session container - we don't need session for this test anyway
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);

                // create store reader with mock transport client, real address selector (that has mock address cache), and real session container
                StoreReader storeReader =
                new StoreReader(mockTransportClient,
                    addressSelector,
                    new AddressEnumerator(),
                    sessionContainer,
                    false);

                Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
                mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                    It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(0));

                // setup max replica set size on the config reader
                ReplicationPolicy replicationPolicy = new ReplicationPolicy
                {
                    MaxReplicaSetSize = 4
                };
                Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
                mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

                QuorumReader reader =
                    new QuorumReader(mockTransportClient, addressSelector, storeReader, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object);

                entity.RequestContext.PerformLocalRefreshOnGoneException = true;
                entity.RequestContext.OriginalRequestConsistencyLevel = Documents.ConsistencyLevel.Strong;
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();

                StoreResponse result = reader.ReadStrongAsync(entity, 2, ReadMode.Strong).Result;
                Assert.IsTrue(result.LSN == 100);

                result.TryGetHeaderValue(WFConstants.BackendHeaders.GlobalCommittedLSN, out string globalCommitedLSN);

                long nGlobalCommitedLSN = long.Parse(globalCommitedLSN, CultureInfo.InvariantCulture);
                Assert.IsTrue(nGlobalCommitedLSN == 90);
            }
        }
    }
}