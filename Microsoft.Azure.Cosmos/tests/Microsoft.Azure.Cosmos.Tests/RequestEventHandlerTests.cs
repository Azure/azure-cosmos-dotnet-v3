//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="SendingRequestEventArgs"/> class.
    /// </summary>
    [TestClass]
    public class RequestEventHandlerTests
    {
        private const string newHeaderKey = "NewlyAddedHeaderKey";
        private const string newHeaderValue = "NewlyAddedHeaderValue";

        /// <summary>
        /// Tests that the event raised on SendingRequestEventArgs acts on the request before processing the request.
        /// </summary>
        [TestMethod]
        public async Task TestServerStoreModelWithRequestEventHandler()
        {
            EventHandler<SendingRequestEventArgs> sendingRequest;
            EventHandler<ReceivedResponseEventArgs> receivedResponse;
            sendingRequest = this.SendingRequestEventHandler;
            receivedResponse = this.ReceivedRequestEventHandler;
            ServerStoreModel storeModel = new ServerStoreModel(this.GetMockStoreClient(), sendingRequest, receivedResponse);

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                        OperationType.Read,
                        ResourceType.Document,
                        AuthorizationTokenType.PrimaryMasterKey))
                {
                    DocumentServiceResponse result = await storeModel.ProcessMessageAsync(request);
                    Assert.IsTrue(request.Headers.Get(newHeaderKey) != null);
                }
            }
        }

        /// <summary>
        /// Tests when no event is raised before processing the request.
        /// </summary>
        [TestMethod]
        public async Task TestServerStoreModelWithNoRequestEventHandler()
        {
            EventHandler<SendingRequestEventArgs> sendingRequest = null;
            EventHandler<ReceivedResponseEventArgs> receivedResponse = null;
            ServerStoreModel storeModel = new ServerStoreModel(this.GetMockStoreClient(), sendingRequest, receivedResponse);

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                        OperationType.Read,
                        ResourceType.Document,
                        AuthorizationTokenType.PrimaryMasterKey))
                {
                    DocumentServiceResponse result = await storeModel.ProcessMessageAsync(request);
                    Assert.IsTrue(request.Headers.Get(newHeaderKey) == null);
                }
            }
        }

        private StoreClient GetMockStoreClient()
        {
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache();
            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            TransportClient mockTransportClient = this.GetMockTransportClient();
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);

            StoreReader storeReader = new StoreReader(mockTransportClient, addressSelector, new AddressEnumerator(), sessionContainer, false);

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

            return new StoreClient(
                        mockAddressCache.Object,
                        sessionContainer,
                        mockServiceConfigReader.Object,
                        mockAuthorizationTokenProvider.Object,
                        Protocol.Tcp,
                        mockTransportClient);
        }

        private TransportClient GetMockTransportClient()
        {
            // create a mock TransportClient
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // setup mock to return respone
            StoreResponse mockStoreResponse = new StoreResponse
            {
                Headers = new StoreResponseNameValueCollection
                {
                    { WFConstants.BackendHeaders.LSN, "110" },
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                }
            };
            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(mockStoreResponse);

            return mockTransportClient.Object;
        }

        private Mock<IAddressResolver> GetMockAddressCache()
        {
            // construct dummy rntbd URIs
            AddressInformation[] addressInformation = new AddressInformation[3];
            for (int i = 0; i <= 2; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0,
                    protocol: Documents.Client.Protocol.Tcp,
                    isPublic: true);
            }

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    new CancellationToken()))
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            return mockAddressCache;
        }

        private void SendingRequestEventHandler(object sender, SendingRequestEventArgs e)
        {
            Assert.IsFalse(e.IsHttpRequest());
            e.DocumentServiceRequest.Headers.Add(newHeaderKey, newHeaderValue);
        }

        private void ReceivedRequestEventHandler(object sender, ReceivedResponseEventArgs e)
        {
            Assert.IsFalse(e.IsHttpResponse());
            Assert.AreEqual(newHeaderValue, e.DocumentServiceRequest.Headers[newHeaderKey]);
            Assert.AreEqual("ACTIVITYID1_1", e.DocumentServiceResponse.Headers[WFConstants.BackendHeaders.ActivityId]);
        }
    }
}