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
    using Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors;
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
        public async Task AddressCacheMockTest()
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
            PartitionAddressInformation addressInfo = await mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken());
            Assert.IsTrue(addressInfo.AllAddresses[0] == addressInformation[0]);
        }

        /// <summary>
        /// Tests for <see cref="TransportClient"/>
        /// </summary>
        [TestMethod]
        public async Task TransportClientMockTest()
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
            StoreResponse response = await mockTransportClientObject.InvokeResourceOperationAsync(new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), entity);

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
        public async Task StoreReaderBarrierTest()
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
            PartitionAddressInformation partitionAddressInformation = await mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken());
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = (await addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/)).Uri;

            // check if the address return from Address Selector matches the original address info
            Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

            // get mock transport client that returns a sequence of responses to simulate upgrade
            TransportClient mockTransportClient = this.GetMockTransportClientDuringUpgrade(addressInformation);

            // get response from mock object
            StoreResponse response = await mockTransportClient.InvokeResourceOperationAsync(new TransportAddressUri(new Uri(addressInformation[0].PhysicalUri)), entity);

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

        /// <summary>
        /// test consistency writer for global strong
        /// </summary>
        [TestMethod]
        public async Task GlobalStrongConsistentWriteMockTest()
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
            PartitionAddressInformation partitionAddressInformation = await mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken());
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = (await addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/)).Uri;

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
                StoreResponse response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);
                Assert.AreEqual(100, response.LSN);

                //globalCommittedLsn never catches up in this case
                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, true, false, false);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                try
                {
                    response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);
                    Assert.Fail();
                }
                catch (Exception)
                {
                }

                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, true, false);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);
                Assert.AreEqual(100, response.LSN);

                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, true, true);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);
                Assert.AreEqual(100, response.LSN);

                mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, i, false, false, true);
                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);
                Assert.AreEqual(100, response.LSN);
            }
        }

        /// <summary>
        /// Mocking Consistency
        /// </summary>
        [TestMethod]
        public async Task GlobalStrongConsistencyMockTest()
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
            PartitionAddressInformation partitionAddressInformation = await mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken());
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = (await addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/)).Uri;

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

                StoreResponse result = await reader.ReadStrongAsync(entity, 2, ReadMode.Strong);
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
                    StoreResponse result = await reader.ReadStrongAsync(entity, 2, ReadMode.Strong);
                    Assert.IsTrue(false);
                }
                catch (GoneException)
                {
                    DefaultTrace.TraceInformation("Gone exception expected!");
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

                StoreResponse result = await reader.ReadStrongAsync(entity, 2, ReadMode.Strong);
                Assert.IsTrue(result.LSN == 100);

                result.TryGetHeaderValue(WFConstants.BackendHeaders.GlobalCommittedLSN, out string globalCommitedLSN);

                long nGlobalCommitedLSN = long.Parse(globalCommitedLSN, CultureInfo.InvariantCulture);
                Assert.IsTrue(nGlobalCommitedLSN == 90);
            }
        }

        private TransportClient GetMockTransportClientForNRegionSynchronousWrites(
            AddressInformation[] addressInformation, bool globalNLsnNeverCatchesUp)
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
                    { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "90" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse2.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "100"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_2" },
                    { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "95" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse3.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "103"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "98" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse4.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "103"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "99" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            mockStoreResponse5.Headers = new StoreResponseNameValueCollection()
                {
                    { WFConstants.BackendHeaders.LSN, "106"},
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_3" },
                    { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "100" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                };

            for (int i = 0; i < addressInformation.Length; i++)
            {

                if (globalNLsnNeverCatchesUp)
                {
                    mockTransportClient.Setup(client => client.InvokeResourceOperationAsync(
                       new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1));

                }
                else
                {
                    mockTransportClient.SetupSequence(client => client.InvokeResourceOperationAsync(
                       new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)), It.IsAny<DocumentServiceRequest>()))
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse1))   // initial write response
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse2))   // barrier retry, count 1
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse3))   // barrier retry, count 2
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse4))   // barrier retry, count 3
                        .Returns(Task.FromResult<StoreResponse>(mockStoreResponse5));  // barrier retry, count 4 GlobalNRegionCommittedGLSN catches up.
                }

            }

            return mockTransportClient.Object;
        }

        /**
        <summary>
        Tests the feature called nregion synchronous commit. This is an account level feature enabled via the accountConfigProperties with property name "EnableNRegionSynchronousCommit"

        Business logic: We send single request to primary of the Write region,which will take care of replicating to its secondaries, one of which is XPPrimary. XPPrimary in this case will replicate this request to n read regions, which will ack from within their region.
            In the write region where the original request was sent to , the request returns from the backend once write quorum number of replicas commits the write - but at this time, the response cannot be returned to caller, since linearizability guarantees will be violated.
            ConsistencyWriter will continuously issue barrier head requests against the partition in question, until GlobalNRegionCommittedGLSN is at least as big as the lsn of the original response.
        Sequence of steps:
        1. After receiving response from primary of write region, look at GlobalNRegionCommittedGLSN and LSN headers.
        2. If GlobalNRegionCommittedGLSN == LSN, return response to caller
        3. If GlobalNRegionCommittedGLSN < LSN && storeResponse.NumberOfReadRegions > 0 , cache LSN in request as SelectedGlobalNRegionCommittedGLSN, and issue barrier requests against any/all replicas.
        4. Each barrier response will contain its own LSN and GlobalNRegionCommittedGLSN, check for any response that satisfies GlobalNRegionCommittedGLSN >= SelectedGlobalNRegionCommittedGLSN
        5. Return to caller on success.
        </summary>
        **/
        [TestMethod]
        public async Task TestWhenNRegionSynchronousCommitEnabledThenDoBarrierHead()
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
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Session.ToString();

            // also setup timeout helper, used in store reader
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(new TimeSpan(2, 2, 2));

            // when the store reader throws Invalid Partition exception, the higher layer should
            // clear this target identity.
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);

            // validate that the mock works
            PartitionAddressInformation partitionAddressInformation = await mockAddressCache.Object.ResolveAsync(entity, false, new CancellationToken());
            IReadOnlyList<AddressInformation> addressInfo = partitionAddressInformation.AllAddresses;

            Assert.IsTrue(addressInfo[0] == addressInformation[0]);

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            Uri primaryAddress = (await addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false /*forceAddressRefresh*/)).Uri;

            // check if the address return from Address Selector matches the original address info
            Assert.IsTrue(primaryAddress.Equals(addressInformation[0].PhysicalUri));

            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReaderVnext> mockServiceConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockServiceConfigReader.Setup(reader => reader.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Session);
            mockServiceConfigReader.Setup(reader => reader.EnableNRegionSynchronousCommit).Returns(true);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            TransportClient mockTransportClient = this.GetMockTransportClientForNRegionSynchronousWrites(addressInformation, false);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);


            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
            StoreResponse response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(3000)), false);
            Assert.AreEqual(100, response.LSN);


            try
            {
                mockTransportClient = this.GetMockTransportClientForNRegionSynchronousWrites(addressInformation, true);
                storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);

                consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);
                response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(3000)), false);
                Assert.Fail();
            }
            catch (DocumentClientException goneEx)
            {
                DefaultTrace.TraceInformation("Gone exception expected!");
                Assert.AreEqual(SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet, goneEx.GetSubStatusCode());
            }
        }

        /// <summary>
        /// When a global strong write barrier times out due to E2E timeout, the resulting
        /// RequestTimeoutException should carry SubStatusCode 21006 (Server_GlobalStrongWriteBarrierNotMet).
        /// </summary>
        [TestMethod]
        public async Task GlobalStrongWriteBarrierTimeout_Returns408With21006()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker()
            };
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);
            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.Setup(reader => reader.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            // GlobalCommittedLSN never catches up — barrier never satisfied, each barrier call delays 200ms
            TransportClient mockTransportClient = this.GetMockTransportClientForBarrierTimeoutTest(addressInformation, BarrierMode.Write);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);

            // Timeout long enough for initial write to complete but expires during barrier retries
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromMilliseconds(500));

            try
            {
                await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromMilliseconds(500)), false);
                Assert.Fail("Expected RequestTimeoutException was not thrown");
            }
            catch (DocumentClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatusCode(),
                    "Expected SubStatusCode 21006 (Server_GlobalStrongWriteBarrierNotMet) for global strong write barrier timeout");
            }
        }

        /// <summary>
        /// When an N-Region synchronous commit barrier times out due to E2E timeout, the resulting
        /// RequestTimeoutException should carry SubStatusCode 21012 (Server_NRegionCommitWriteBarrierNotMet).
        /// </summary>
        [TestMethod]
        public async Task NRegionCommitBarrierTimeout_Returns408With21012()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker()
            };
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Session.ToString();
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);
            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReaderVnext> mockServiceConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockServiceConfigReader.Setup(reader => reader.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Session);
            mockServiceConfigReader.Setup(reader => reader.EnableNRegionSynchronousCommit).Returns(true);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            // GlobalNRegionCommittedGLSN never catches up — barrier never satisfied, each barrier call delays 200ms
            TransportClient mockTransportClient = this.GetMockTransportClientForBarrierTimeoutTest(addressInformation, BarrierMode.WriteNRegion);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);

            // Timeout long enough for initial write to complete but expires during barrier retries
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromMilliseconds(500));

            try
            {
                await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromMilliseconds(500)), false);
                Assert.Fail("Expected RequestTimeoutException was not thrown");
            }
            catch (DocumentClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                Assert.AreEqual(SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet, ex.GetSubStatusCode(),
                    "Expected SubStatusCode 21012 (Server_NRegionCommitWriteBarrierNotMet) for N-Region commit barrier timeout");
            }
        }

        /// <summary>
        /// When barrier retries are exhausted (not timeout), the existing behavior should remain
        /// unchanged: GoneException (503) with the correct sub-status code.
        /// </summary>
        [TestMethod]
        public async Task GlobalStrongWriteBarrierRetriesExhausted_Returns503WithSubStatus()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker()
            };
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30));
            entity.RequestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange();

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);
            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.Setup(reader => reader.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            // GlobalCommittedLSN never catches up — barrier retries will be exhausted
            TransportClient mockTransportClient = this.GetMockTransportClientForGlobalStrongWrites(addressInformation, 0, true, false, false);
            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector, sessionContainer, mockTransportClient, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object, false, false);

            try
            {
                // Long timeout — retries exhaust before timeout fires
                await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);
                Assert.Fail("Expected GoneException was not thrown");
            }
            catch (DocumentClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatusCode(),
                    "Expected SubStatusCode 21006 (Server_GlobalStrongWriteBarrierNotMet) for barrier retry exhaustion");
            }
        }

        /// <summary>
        /// When a global strong read barrier times out due to E2E timeout, the resulting
        /// GoneException should carry SubStatusCode 21014 (Server_ReadBarrierFailed).
        /// </summary>
        [TestMethod]
        public async Task GlobalStrongReadBarrierTimeout_Returns410With21014()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                PerformLocalRefreshOnGoneException = true,
                OriginalRequestConsistencyLevel = Documents.ConsistencyLevel.Strong,
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                QuorumSelectedLSN = -1,
                GlobalCommittedSelectedLSN = -1,
                TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false),
                ResolvedPartitionKeyRange = new PartitionKeyRange()
            };

            AddressInformation[] addressInformation = this.GetMockAddressInformationDuringUpgrade();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);
            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            // Read replicas return LSN=100 (quorum met on LSN) but GlobalCommittedLSN=90 stays
            // behind indefinitely, and each HEAD barrier response is delayed so the E2E
            // TimeoutHelper fires inside WaitForReadBarrierAsync → ThrowGoneIfElapsed(Server_ReadBarrierFailed).
            TransportClient mockTransportClient = this.GetMockTransportClientForBarrierTimeoutTest(addressInformation, BarrierMode.Read);
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            ReplicationPolicy replicationPolicy = new ReplicationPolicy { MaxReplicaSetSize = 4 };
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

            QuorumReader reader = new QuorumReader(
                mockTransportClient, addressSelector, storeReader, mockServiceConfigReader.Object, mockAuthorizationTokenProvider.Object);

            // Short timeout: guaranteed to expire inside the barrier retry loop (each HEAD is delayed 200ms).
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromMilliseconds(500));

            try
            {
                await reader.ReadStrongAsync(entity, 2, ReadMode.Strong);
                Assert.Fail("Expected GoneException was not thrown");
            }
            catch (DocumentClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                Assert.AreEqual(SubStatusCodes.Server_ReadBarrierFailed, ex.GetSubStatusCode(),
                    "Expected SubStatusCode 21014 (Server_ReadBarrierFailed) for read barrier timeout");
            }
        }

        /// <summary>
        /// Creates a mock transport client where the initial write response has a lagging global committed LSN
        /// (triggering the barrier), and each barrier HEAD request introduces a delay so the E2E timeout fires
        /// during the barrier retry loop rather than before or after it.
        /// </summary>
        private enum BarrierMode
        {
            Write,
            WriteNRegion,
            Read,
        }

        /// <summary>
        /// Creates a mock transport client for a barrier-timeout scenario:
        /// - The initial call on each replica returns LSN=100 but with the tracked "global"
        ///   marker (GlobalCommittedLSN for write / read barriers, GlobalNRegionCommittedGLSN
        ///   for the N-region write barrier) held at 90 so the barrier is not met and the
        ///   SDK enters the barrier retry loop.
        /// - Every subsequent HEAD barrier request keeps the marker at 90 and delays 200ms,
        ///   so the E2E TimeoutHelper fires inside the barrier wait loop.
        /// </summary>
        private TransportClient GetMockTransportClientForBarrierTimeoutTest(
            AddressInformation[] addressInformation,
            BarrierMode mode)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            StoreResponse initialResponse = new StoreResponse
            {
                Headers = BuildBarrierHeaders(mode, activityId: "ACTIVITYID_INITIAL")
            };

            StoreResponse barrierResponse = new StoreResponse
            {
                Headers = BuildBarrierHeaders(mode, activityId: "ACTIVITYID_BARRIER")
            };

            int callCount = 0;
            for (int i = 0; i < addressInformation.Length; i++)
            {
                mockTransportClient.Setup(
                    client => client.InvokeResourceOperationAsync(
                        new TransportAddressUri(new Uri(addressInformation[i].PhysicalUri)),
                        It.IsAny<DocumentServiceRequest>()))
                    .Returns(async (TransportAddressUri uri, DocumentServiceRequest req) =>
                    {
                        int currentCall = System.Threading.Interlocked.Increment(ref callCount);
                        if (currentCall <= addressInformation.Length)
                        {
                            // First call per replica: initial response (instant)
                            return initialResponse;
                        }

                        // Subsequent calls: barrier HEAD — delay so the E2E timeout fires here.
                        await Task.Delay(200);
                        return barrierResponse;
                    });
            }

            return mockTransportClient.Object;
        }

        private static StoreResponseNameValueCollection BuildBarrierHeaders(BarrierMode mode, string activityId)
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection
            {
                { WFConstants.BackendHeaders.LSN, "100" },
                { WFConstants.BackendHeaders.ActivityId, activityId },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
            };

            if (mode == BarrierMode.WriteNRegion)
            {
                headers.Add(WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "90");
                headers.Add(WFConstants.BackendHeaders.GlobalCommittedLSN, "100");
            }
            else
            {
                headers.Add(WFConstants.BackendHeaders.GlobalCommittedLSN, "90");
            }

            return headers;
        }
    }
}