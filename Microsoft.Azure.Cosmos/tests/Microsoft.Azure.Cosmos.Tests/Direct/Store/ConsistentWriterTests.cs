//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ConsistentWriterTests
    {

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task GlobalStrongWrite_ThrowsTimeoutOnRegionalFailoverAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            Uri originalEndpoint = new Uri("https://original-region.documents.azure.com");
            Uri failoverEndpoint = new Uri("https://failover-region.documents.azure.com");

            // Simulate a retry of a barrier check after a regional failover
            setup.Entity.RequestContext.CachedWriteStoreResult = StoreResult.CreateForTesting(new StoreResponse(), numberOfReadRegions: 3);
            setup.Entity.RequestContext.GlobalStrongWriteEndpoint = originalEndpoint;
            setup.Entity.RequestContext.RouteToLocation(failoverEndpoint);
            setup.Entity.RequestContext.GlobalCommittedSelectedLSN = 100;

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup);

            // Act & Assert
            try
            {
                await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
                Assert.Fail("Expected InternalServerErrorException was not thrown on regional failover.");
            }
            catch (RequestTimeoutException ex)
            {
                Assert.AreEqual(SubStatusCodes.WriteRegionBarrierChangedMidOperation, ex.GetSubStatus(), "Unexpected substatus code for failover ");
                Assert.IsTrue(ex.Message.Contains("The write operation was initiated in region with endpoint"), "Exception message should indicate a regional failover.");
                Assert.IsTrue(ex.Message.Contains(originalEndpoint.ToString()), "Exception message should contain the original endpoint.");
                Assert.IsTrue(ex.Message.Contains(failoverEndpoint.ToString()), "Exception message should contain the failover endpoint.");
            }
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task GlobalStrongWrite_SucceedsOnBarrierRetryInSameRegionAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            Uri writeEndpoint = new Uri("https://write-region.documents.azure.com");
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // First barrier call fails to meet LSN, second one succeeds
                        string globalCommittedLsn = barrierRequestCount == 1 ? "99" : "101";
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, globalCommittedLsn },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    // Initial write request
                    req.RequestContext.RouteToLocation(writeEndpoint);
                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    };
                });

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup);

            // Act
            StoreResponse response = await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);

            // Assert
            Assert.AreEqual(200, response.Status, "Expected a successful response.");
            Assert.AreEqual(2, barrierRequestCount, "Expected two barrier requests to be made.");
            Assert.AreEqual(writeEndpoint, setup.Entity.RequestContext.GlobalStrongWriteEndpoint, "GlobalStrongWriteEndpoint should be set to the write endpoint.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RequestTimeoutException_RetainsSubStatusCodeOnBarrierFailureAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            Uri originalEndpoint = new Uri("https://original-region.documents.azure.com");
            Uri failoverEndpoint = new Uri("https://failover-region.documents.azure.com");

            // Simulate a retry of a barrier check after a regional failover
            setup.Entity.RequestContext.CachedWriteStoreResult = StoreResult.CreateForTesting(new StoreResponse(), numberOfReadRegions: 3);
            setup.Entity.RequestContext.GlobalStrongWriteEndpoint = originalEndpoint;
            setup.Entity.RequestContext.RouteToLocation(failoverEndpoint);
            setup.Entity.RequestContext.GlobalCommittedSelectedLSN = 100;

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup);

            // Act & Assert
            try
            {
                await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
                Assert.Fail("Expected RequestTimeoutException was not thrown.");
            }
            catch (RequestTimeoutException ex)
            {
                // The substatus code should be retained and not Unknown
                Assert.AreEqual(SubStatusCodes.WriteRegionBarrierChangedMidOperation, ex.GetSubStatus(), "Substatus code should be retained and not Unknown.");
                Assert.IsTrue(ex.Message.Contains("The write operation was initiated in region with endpoint"), "Exception message should indicate a regional failover.");
                Assert.IsTrue(ex.Message.Contains(originalEndpoint.ToString()), "Exception message should contain the original endpoint.");
                Assert.IsTrue(ex.Message.Contains(failoverEndpoint.ToString()), "Exception message should contain the failover endpoint.");
            }
        }

        private static readonly AddressInformation[] CommonAddressInformation = new AddressInformation[]
        {
                 new AddressInformation("rntbd://replica1", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                 new AddressInformation("rntbd://replica2", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                 new AddressInformation("rntbd://replica3", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
        };

        private static ConsistencyWriterTestSetup CreateTestSetup()
        {
            // Create DocumentServiceRequest
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";

            // Set request charge tracker and timeout helper
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(new TimeSpan(0, 0, 30))
            };

            requestContext.TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false);
            entity.RequestContext = requestContext;

            // Mock AddressResolver
            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Default);
            mockAddressResolver
                .Setup(x => x.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(CommonAddressInformation));

            // Mock TransportClient
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Default);

            // Mock dependencies for ConsistencyWriter
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);
            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            Mock<AccountConfigurationProperties> mockAccountConfigurationProperties = new Mock<AccountConfigurationProperties>(false);

            return new ConsistencyWriterTestSetup
            {
                Entity = entity,
                MockAddressResolver = mockAddressResolver,
                MockTransportClient = mockTransportClient,
                MockServiceConfigReader = mockServiceConfigReader,
                MockAuthorizationTokenProvider = mockAuthorizationTokenProvider,
                MockAccountConfigurationProperties = mockAccountConfigurationProperties
            };
        }

        private static ConsistencyWriter CreateConsistencyWriter(ConsistencyWriterTestSetup setup)
        {
            return new ConsistencyWriter(
                new AddressSelector(setup.MockAddressResolver.Object, Protocol.Tcp),
                new NullSessionContainer(),
                setup.MockTransportClient.Object,
                setup.MockServiceConfigReader.Object,
                setup.MockAuthorizationTokenProvider.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false);
        }

        // Helper class to organize test setup data
        private class ConsistencyWriterTestSetup
        {
            public DocumentServiceRequest Entity { get; set; }

            public Mock<IAddressResolver> MockAddressResolver { get; set; }

            public Mock<TransportClient> MockTransportClient { get; set; }

            public Mock<IServiceConfigurationReader> MockServiceConfigReader { get; set; }

            public Mock<IAuthorizationTokenProvider> MockAuthorizationTokenProvider { get; set; }

            public Mock<AccountConfigurationProperties> MockAccountConfigurationProperties { get; set; }
        }

    }
}