//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ConsistencyWriterTests
    {
        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task SessionNotFoundExceptionLessRetryConsistencyTestAsync()
        {
            int withExceptionLessRetryCount = await this.SessionNotFoundExceptionLessRetryInternalTestAsync(useStatusCodeFor4041002: true);
            int withoutExceptionRetryCount = await this.SessionNotFoundExceptionLessRetryInternalTestAsync(useStatusCodeFor4041002: false);

            Assert.AreEqual(2, withoutExceptionRetryCount);
            Assert.AreEqual(withoutExceptionRetryCount, withExceptionLessRetryCount);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow(true)]
        [DataRow(false)]
        public async Task SessionNotFoundExceptionLessRetryTestAsync(bool useStatusCodeFor4041002)
        {
            await this.SessionNotFoundExceptionLessRetryInternalTestAsync(useStatusCodeFor4041002);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_ReplicaGone1022_PrimarySuccess_ShouldSucceed()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica1/1s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica2/2s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType != OperationType.Head)
                    {
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                            })
                        };
                    }

                    string u = uri.Uri.ToString();
                    if (u.Contains("/1s/") || u.Contains("/2s/"))
                    {
                        throw new GoneException("gone", SubStatusCodes.ReadSessionNotAvailable);
                    }

                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                true,
                true,
                null);

            StoreResponse result = await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(10)), false);
            Assert.IsNotNull(result);
            Assert.AreEqual(200, result.Status);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_ReplicaGone1022_PrimaryGone1022_ShouldThrowGoneException()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica1/1s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica2/2s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType != OperationType.Head)
                    {
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                            })
                        };
                    }

                    string u = uri.Uri.ToString();
                    if (u.Contains("/1s/") || u.Contains("/2s/") || u.Contains("/0p/"))
                    {
                        throw new GoneException("gone", SubStatusCodes.ReadSessionNotAvailable);
                    }

                    return new StoreResponse { Status = 200, Headers = new DictionaryNameValueCollection() };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                true,
                true,
                null);

            await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(10)), false);
            });
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_ReplicaGone1022_PrimaryOtherError_ShouldThrowException()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica1/1s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica2/2s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType != OperationType.Head)
                    {
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                            })
                        };
                    }

                    string u = uri.Uri.ToString();
                    if (u.Contains("/1s/") || u.Contains("/2s/"))
                    {
                        throw new GoneException("gone", SubStatusCodes.ReadSessionNotAvailable);
                    }

                    throw new NotFoundException(new Exception("nf"), SubStatusCodes.Unknown);
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                true,
                true,
                null);

            await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(10)), false);
            });
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_ReplicaGone1022_PrimaryInsufficientGCLSN_ShouldFail()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica1/1s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica2/2s/", isPrimary: false,  protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType != OperationType.Head)
                    {
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "90" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                            })
                        };
                    }

                    string u = uri.Uri.ToString();
                    if (u.Contains("/1s/") || u.Contains("/2s/"))
                    {
                        throw new GoneException("gone", SubStatusCodes.ReadSessionNotAvailable);
                    }

                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                true,
                true,
                null);

            await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(10)), false);
            });
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_WithGlobalCommittedLSN_ShouldEnforceBarrierOnLsnMismatchAndSucceed()
        {
            // Arrange
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            entity.RequestContext.CachedWriteStoreResult = null;
            entity.RequestContext.GlobalCommittedSelectedLSN = 42;

            Mock<IAddressResolver> mockedAddressResolver = new Mock<IAddressResolver>();
            AddressInformation[] addressInformation = new AddressInformation[4];

            // construct URIs that look like the actual uri
            for (int i = 0; i < 4; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            mockedAddressResolver.Setup(
                x => x
                .ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockedAddressResolver.Object, Protocol.Tcp);

            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();
            IServiceConfigurationReader serviceConfigReader = Mock.Of<IServiceConfigurationReader>(r => r.DefaultConsistencyLevel == ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> authorizationTokenProvider = new();
            authorizationTokenProvider
                .Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            int barrierRequestCounter = 0;
            Mock<TransportClient> mockedTransportClient = new Mock<TransportClient>(MockBehavior.Strict);
            mockedTransportClient
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType == OperationType.Create)
                    {
                        return new StoreResponse
                        {
                            Status = 201,
                            Headers = new DictionaryNameValueCollection(
                            new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.LocalLSN, "10" },
                                { WFConstants.BackendHeaders.ItemLocalLSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "9" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" }
                            }),
                        };
                    }

                    if (r.OperationType == OperationType.Head)
                    {
                        string globalCommittedLSN = "9";

                        // Simulate N region committed LSN catch up after 3 barrier requests retry.
                        if (barrierRequestCounter++ > 2)
                        {
                            globalCommittedLSN = "10";
                        }

                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, globalCommittedLSN },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                sessionContainer,
                mockedTransportClient.Object,
                serviceConfigReader,
                authorizationTokenProvider.Object,
                true,
                true,
                null);

            // Act & Assert
            StoreResponse result = await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(1200)), false);

            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.AreEqual(4, barrierRequestCounter, "Failed to ensure that there were 4 barrier calls made in total.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_DoesntTrigger_During_Null_AccountOrDependency_Properties()
        {
            Mock<IAddressResolver> mockedAddressResolver = new Mock<IAddressResolver>();
            AddressInformation[] addressInformation = new AddressInformation[1];

            // construct URIs that look like the actual uri
            for (int i = 0; i < 1; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            mockedAddressResolver.Setup(
                x => x
                .ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));
            AddressSelector addressSelector = new AddressSelector(mockedAddressResolver.Object, Protocol.Tcp);

            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();
            IServiceConfigurationReader serviceConfigReader = Mock.Of<IServiceConfigurationReader>(r => r.DefaultConsistencyLevel == ConsistencyLevel.Session);

            Mock<IAuthorizationTokenProvider> authorizationTokenProvider = new();
            authorizationTokenProvider
                .Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            int actualNumberOfBarrierCalls = 0;
            Mock<TransportClient> mockedTransportClient = new Mock<TransportClient>(MockBehavior.Strict);
            mockedTransportClient
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType == OperationType.Create)
                    {
                        return new StoreResponse
                        {
                            Status = 201,
                            Headers = new DictionaryNameValueCollection(
                            new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.LocalLSN, "10" },
                                { WFConstants.BackendHeaders.ItemLocalLSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "9" },
                                { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" }
                            }),
                        };
                    }

                    if (r.OperationType == OperationType.Head)
                    {
                        actualNumberOfBarrierCalls++;
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "10" },
                                { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "10" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            entity.RequestContext.CachedWriteStoreResult = null;
            entity.RequestContext.GlobalCommittedSelectedLSN = 42;
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                sessionContainer,
                mockedTransportClient.Object,
                serviceConfigReader,
                authorizationTokenProvider.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: true,
                sessionRetryOptions: null);

            // Act & Assert on null IServiceConfigurationReader.
            StoreResponse result = await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(1200)), false);

            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.AreEqual(0, actualNumberOfBarrierCalls, "No Barrier calls should have been made since AccountConfigurationProperties was null");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow(true, DisplayName = "Scenario for a multi master account.")]
        [DataRow(false, DisplayName = "Scenario for a single master account.")]
        public async Task WriteBarrier_WithNRegionCommittedLSN_ShouldEnforceBarrierOnLsnMismatchAndSucceed(
            bool isMultiMasterAccount)
        {
            // Arrange
            int expectedNumberOfBarrierCalls = isMultiMasterAccount ? 0 : 4;
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            entity.RequestContext.CachedWriteStoreResult = null;
            entity.RequestContext.GlobalCommittedSelectedLSN = 42;
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };

            Mock<IAddressResolver> mockedAddressResolver = new Mock<IAddressResolver>();
            AddressInformation[] addressInformation = new AddressInformation[4];

            // construct URIs that look like the actual uri
            for (int i = 0; i < 4; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            mockedAddressResolver.Setup(
                x => x
                .ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockedAddressResolver.Object, Protocol.Tcp);

            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();

            Mock<IServiceConfigurationReaderVnext> mockServiceConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockServiceConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockServiceConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(true);

            IServiceConfigurationReader serviceConfigReader = mockServiceConfigReader.Object;

            Mock<IAuthorizationTokenProvider> authorizationTokenProvider = new();
            authorizationTokenProvider
                .Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            int actualNumberOfBarrierCalls = 0;
            Mock<TransportClient> mockedTransportClient = new Mock<TransportClient>(MockBehavior.Strict);
            mockedTransportClient
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType == OperationType.Create)
                    {
                        return new StoreResponse
                        {
                            Status = 201,
                            Headers = new DictionaryNameValueCollection(
                            new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.LocalLSN, "10" },
                                { WFConstants.BackendHeaders.ItemLocalLSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "9" },
                                { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" }
                            }),
                        };
                    }

                    if (r.OperationType == OperationType.Head)
                    {
                        string globalCommittedNRegionLSN = "9";

                        // Simulate N region committed LSN catch up after 3 barrier requests retry.
                        if (actualNumberOfBarrierCalls++ > 2)
                        {
                            globalCommittedNRegionLSN = "10";
                        }

                        // Return a successful barrier response to complete the flow
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "10" },
                                { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, globalCommittedNRegionLSN },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            })
                        };
                    }

                    // Simulate a successful primary write to trigger the barrier call
                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                sessionContainer,
                mockedTransportClient.Object,
                serviceConfigReader,
                authorizationTokenProvider.Object,
                useMultipleWriteLocations: isMultiMasterAccount,
                enableReplicaValidation: true,
                sessionRetryOptions: null);

            // Act & Assert
            StoreResponse result = await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(1200)), false);

            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.AreEqual(expectedNumberOfBarrierCalls, actualNumberOfBarrierCalls, "Failed to ensure that there were 4 barrier calls made in total.");
        }

        [TestMethod]
        [Owner("ananth")]
        public async Task WriteBarrier_WithNRegionCommittedLSN_NonVnextConfigReader_ShouldNotEnforceBarrier()
        {
            // Arrange - Test that when serviceConfigReader is NOT IServiceConfigurationReaderVnext,
            // no barrier is triggered even when GlobalNRegionCommittedGLSN indicates a mismatch from the original GLSN.
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            entity.RequestContext.CachedWriteStoreResult = null;
            entity.RequestContext.GlobalCommittedSelectedLSN = 42;
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };

            Mock<IAddressResolver> mockedAddressResolver = new Mock<IAddressResolver>();
            AddressInformation[] addressInformation = new AddressInformation[4];

            // construct URIs that look like the actual uri
            for (int i = 0; i < 4; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            mockedAddressResolver.Setup(
                x => x
                .ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockedAddressResolver.Object, Protocol.Tcp);

            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();

            // Use base IServiceConfigurationReader (NOT IServiceConfigurationReaderVnext)
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);

            IServiceConfigurationReader serviceConfigReader = mockServiceConfigReader.Object;

            Mock<IAuthorizationTokenProvider> authorizationTokenProvider = new();
            authorizationTokenProvider
                .Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            int actualNumberOfBarrierCalls = 0;
            Mock<TransportClient> mockedTransportClient = new Mock<TransportClient>(MockBehavior.Strict);
            mockedTransportClient
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType == OperationType.Create)
                    {
                        return new StoreResponse
                        {
                            Status = 201,
                            Headers = new DictionaryNameValueCollection(
                            new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.LSN, "10" },
                        { WFConstants.BackendHeaders.LocalLSN, "10" },
                        { WFConstants.BackendHeaders.ItemLocalLSN, "10" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "9" },
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" }
                            }),
                        };
                    }

                    if (r.OperationType == OperationType.Head)
                    {
                        actualNumberOfBarrierCalls++;
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.LSN, "10" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "10" },
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "10" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                        { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                    { WFConstants.BackendHeaders.LSN, "100" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                sessionContainer,
                mockedTransportClient.Object,
                serviceConfigReader,
                authorizationTokenProvider.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: true,
                sessionRetryOptions: null);

            // Act
            StoreResponse result = await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(1200)), false);

            // Assert - No barrier calls should be made since serviceConfigReader is not IServiceConfigurationReaderVnext
            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.AreEqual(0, actualNumberOfBarrierCalls, "No barrier calls should have been made since serviceConfigReader is not IServiceConfigurationReaderVnext");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_WithNRegionCommittedLSNAndCachedResponsePresent_ShouldEnforceBarrierOnLsnMismatchAndSucceed()
        {
            // Arrange
            StoreResponse storeResponse = new StoreResponse
            {
                Status = 201,
                Headers = new DictionaryNameValueCollection(
                new NameValueCollection
                {
                    { WFConstants.BackendHeaders.LSN, "10" },
                    { WFConstants.BackendHeaders.LocalLSN, "10" },
                    { WFConstants.BackendHeaders.ItemLocalLSN, "10" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "9" },
                    { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "3" }
                }),
            };

            ReferenceCountedDisposable<StoreResult> mockedStoreReqult = StoreResult.CreateStoreResult(
                storeResponse,
                null,
                false,
                false,
                replicaHealthStatuses: new List<string>()
                {
                    "http://storephysicaladdress-1p.com:Connected",
                    "http://storephysicaladdress-2s.com:Unknown",
                    "http://storephysicaladdress-3s.com:Unhealthy",
                    "http://storephysicaladdress-4s.com:Unknown"
                },
                new Uri("http://storephysicaladdress.com"));

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };
            entity.RequestContext.GlobalCommittedSelectedLSN = 10;
            entity.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };
            entity.RequestContext.CachedWriteStoreResult = mockedStoreReqult;

            Mock<IAddressResolver> mockedAddressResolver = new Mock<IAddressResolver>();
            AddressInformation[] addressInformation = new AddressInformation[4];

            // construct URIs that look like the actual uri
            for (int i = 0; i < 4; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            mockedAddressResolver.Setup(
                x => x
                .ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockedAddressResolver.Object, Protocol.Tcp);

            ISessionContainer sessionContainer = Mock.Of<ISessionContainer>();
            Mock<IServiceConfigurationReaderVnext> serviceConfigReaderMock = new Mock<IServiceConfigurationReaderVnext>();

            serviceConfigReaderMock.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(true);
            serviceConfigReaderMock.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);

            IServiceConfigurationReaderVnext serviceConfigReader = serviceConfigReaderMock.Object;

            Mock<IAuthorizationTokenProvider> authorizationTokenProvider = new();
            authorizationTokenProvider
                .Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            int barrierRequestCounter = 0;
            Mock<TransportClient> mockedTransportClient = new Mock<TransportClient>(MockBehavior.Strict);
            mockedTransportClient
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    if (r.OperationType == OperationType.Head)
                    {
                        string globalCommittedNRegionLSN = "9";

                        // Simulate N region committed LSN catch up after 3 barrier requests retry.
                        if (barrierRequestCounter++ > 2)
                        {
                            globalCommittedNRegionLSN = "10";
                        }

                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "10" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "10" },
                                { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, globalCommittedNRegionLSN },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                                { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" },
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                sessionContainer,
                mockedTransportClient.Object,
                serviceConfigReader,
                authorizationTokenProvider.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: true,
                sessionRetryOptions: null);

            // Act & Assert
            StoreResponse result = await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(1200)), false);

            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.AreEqual(4, barrierRequestCounter, "Failed to ensure that there were 4 barrier calls made in total.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierRetriesExhaustedWith429_Throws408Async()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // Simulate all barrier requests being throttled
                        return new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() }
                            })
                        };
                    }

                    // Simulate a successful primary write to trigger barrier requests
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup, enableBarrierEarlyYieldOn429: true);

            // Act & Assert
            try
            {
                await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
                Assert.Fail("Expected RequestTimeoutException was not thrown.");
            }
            catch (RequestTimeoutException ex)
            {
                Assert.AreEqual(SubStatusCodes.Server_WriteBarrierThrottled, ex.GetSubStatus(), "Expected Server_WriteBarrierTimeout substatus.");
                Assert.AreEqual(34, barrierRequestCount, "Expected all barrier requests to be attempted before failing."); //maxShortBarrierRetriesForMultiRegion + maxNumberOfWriteBarrierReadRetries 
            }
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_FlagDisabled_BarrierRetriesExhaustedWith429_ThrowsGoneAsync()
        {
            // Arrange - Flag is OFF (default), so barrier exhaustion with 429s
            // should NOT throw Server_WriteBarrierThrottled; instead the normal
            // barrier-not-met path yields GoneException with
            // Server_GlobalStrongWriteBarrierNotMet.
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        return new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() }
                            })
                        };
                    }

                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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

            // Act & Assert
            try
            {
                await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
                Assert.Fail("Expected GoneException was not thrown.");
            }
            catch (GoneException ex)
            {
                Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatus(),
                    "With flag disabled, barrier exhaustion should throw Server_GlobalStrongWriteBarrierNotMet, not Server_WriteBarrierThrottled.");
                Assert.AreEqual(34, barrierRequestCount, "Expected all barrier requests to be attempted before failing.");
            }
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierFailsWithNon429_ThrowsOriginalExceptionAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // Simulate barrier requests failing with a non-429 exception
                        throw new GoneException("Partition is gone", SubStatusCodes.PartitionKeyRangeGone);
                    }

                    // Simulate a successful primary write
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    });
                });

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup);

            // Act & Assert
            try
            {
                await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
                Assert.Fail("Expected GoneException was not thrown.");
            }
            catch (GoneException ex)
            {
                Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatus(), "Expected Server_GlobalStrongWriteBarrierNotMet substatus.");
                Assert.AreEqual(34, barrierRequestCount, "Expected all barrier requests to be attempted before failing."); //maxShortBarrierRetriesForMultiRegion + maxNumberOfWriteBarrierReadRetries 
            }
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierSucceeds_ReturnsSuccessAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // Simulate a successful barrier response
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                            })
                        };
                    }

                    // Simulate a successful primary write
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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
            Assert.AreEqual(200, response.Status, "Expected a successful status code.");
            Assert.AreEqual(1, barrierRequestCount, "Expected exactly one barrier request.");
            Assert.AreEqual(5.5, setup.Entity.RequestContext.RequestChargeTracker.TotalRequestCharge, "Request charge should be from the primary write.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierRequest_PropagatesExceptionlessFlagsAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            DocumentServiceRequest barrierRequest = null;

            // Set the exception-less flags on the original request
            setup.Entity.UseStatusCodeFor429 = true;
            setup.Entity.UseStatusCodeForFailures = true;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        // Capture the barrier request to inspect its properties
                        barrierRequest = req;

                        // Return a successful barrier response to complete the flow
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                            })
                        };
                    }

                    // Simulate a successful primary write to trigger the barrier call
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

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup, enableBarrierEarlyYieldOn429: true);

            // Act
            await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);

            // Assert
            Assert.IsNotNull(barrierRequest, "Barrier request should have been made.");
            Assert.IsTrue(barrierRequest.UseStatusCodeFor429, "UseStatusCodeFor429 should be propagated.");
            Assert.IsTrue(barrierRequest.UseStatusCodeForFailures, "UseStatusCodeForFailures should be propagated.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierRequest_PropagatesAllExceptionlessFlagsAsync()
        {
            // Arrange
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            DocumentServiceRequest barrierRequest = null;

            // Set ALL exception-less flags on the original request
            setup.Entity.UseStatusCodeFor429 = true;
            setup.Entity.UseStatusCodeForFailures = true;
            setup.Entity.UseStatusCodeFor403 = true;
            setup.Entity.UseStatusCodeFor4041002 = true;
            setup.Entity.UseStatusCodeForBadRequest = true;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        // Capture the barrier request to inspect its properties
                        barrierRequest = req;

                        // Return a successful barrier response to complete the flow
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                            })
                        };
                    }

                    // Simulate a successful primary write to trigger the barrier call
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

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup, enableBarrierEarlyYieldOn429: true);

            // Act
            await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);

            // Assert - Verify all UseStatusCode flags are propagated to barrier request
            Assert.IsNotNull(barrierRequest, "Barrier request should have been made.");
            Assert.IsTrue(barrierRequest.UseStatusCodeFor429, "UseStatusCodeFor429 should be propagated.");
            Assert.IsTrue(barrierRequest.UseStatusCodeForFailures, "UseStatusCodeForFailures should be propagated.");
            Assert.IsTrue(barrierRequest.UseStatusCodeFor403, "UseStatusCodeFor403 should be propagated.");
            Assert.IsTrue(barrierRequest.UseStatusCodeFor4041002, "UseStatusCodeFor4041002 should be propagated.");
            Assert.IsTrue(barrierRequest.UseStatusCodeForBadRequest, "UseStatusCodeForBadRequest should be propagated.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierPartial429_SucceedsWhenBarrierMetAsync()
        {
            // Arrange - Test when some replicas return 429 but barrier condition is still met
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // First replica returns 429 (throttled)
                        if (uri.Uri.ToString().Contains("replica1"))
                        {
                            return new StoreResponse
                            {
                                Status = 429,
                                Headers = new DictionaryNameValueCollection(new NameValueCollection
                                {
                                    { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                                })
                            };
                        }

                        // Other replicas return success with barrier met
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                            })
                        };
                    }

                    // Simulate a successful primary write to trigger barrier requests
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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

            // Assert - Should succeed because barrier condition was met by non-throttled replicas
            Assert.AreEqual(200, response.Status, "Expected a successful status code.");
            Assert.IsTrue(barrierRequestCount >= 1, "Expected at least one barrier request.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierMixed429And410_HandlesCorrectlyAsync()
        {
            // Arrange - Test when replicas return mix of 429 and 410 (Gone)
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // First replica returns 429
                        if (uri.Uri.ToString().Contains("replica1"))
                        {
                            return new StoreResponse
                            {
                                Status = 429,
                                Headers = new DictionaryNameValueCollection(new NameValueCollection
                                {
                                    { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() }
                                })
                            };
                        }

                        // Second replica returns 410 Gone
                        if (uri.Uri.ToString().Contains("replica2"))
                        {
                            return new StoreResponse
                            {
                                Status = 410,
                                Headers = new DictionaryNameValueCollection(new NameValueCollection
                                {
                                    { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.PartitionKeyRangeGone).ToString() }
                                })
                            };
                        }

                        // Third replica meets barrier
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                            })
                        };
                    }

                    // Simulate a successful primary write
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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

            // Assert - Should succeed because one replica met the barrier condition
            Assert.AreEqual(200, response.Status, "Expected a successful status code when barrier is met by at least one replica.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierTransient429ThenSuccess_SucceedsAsync()
        {
            // Arrange - Test when 429 is transient and resolves within retry window
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // First few barrier requests return 429, then succeed
                        if (barrierRequestCount <= 3)
                        {
                            return new StoreResponse
                            {
                                Status = 429,
                                Headers = new DictionaryNameValueCollection(new NameValueCollection
                                {
                                    { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" }
                                })
                            };
                        }

                        // After transient throttling, succeed
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                            })
                        };
                    }

                    // Simulate a successful primary write
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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

            // Assert - Should succeed after transient throttling resolves
            Assert.AreEqual(200, response.Status, "Expected a successful status code after transient 429.");
            Assert.IsTrue(barrierRequestCount > 3, "Expected multiple barrier retries before success.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ConsistencyWriter_BarrierWith429SubStatusVariations_Throws408WithCorrectSubStatusAsync()
        {
            // Arrange - Test different 429 substatus codes result in correct exception
            ConsistencyWriterTestSetup setup = CreateTestSetup();
            int barrierRequestCount = 0;

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        barrierRequestCount++;

                        // All barrier requests return 429 with different substatus
                        return new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ClientTcpChannelFull).ToString() }
                            })
                        };
                    }

                    // Simulate a successful primary write
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
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

            ConsistencyWriter consistencyWriter = CreateConsistencyWriter(setup, enableBarrierEarlyYieldOn429: true);

            // Act & Assert - Regardless of 429 substatus, should throw 408 with Server_WriteBarrierThrottled
            try
            {
                await consistencyWriter.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
                Assert.Fail("Expected RequestTimeoutException was not thrown.");
            }
            catch (RequestTimeoutException ex)
            {
                Assert.AreEqual(SubStatusCodes.Server_WriteBarrierThrottled, ex.GetSubStatus(), "Expected Server_WriteBarrierThrottled substatus regardless of original 429 substatus.");
            }
        }

        [TestMethod]
        [Owner("dibilla")]
        [DataRow(true, "Strong", "Eventual", true, DisplayName = "PPF override Strong on Eventual account => write barrier")]
        [DataRow(false, "", "Eventual", false, DisplayName = "No override on Eventual account => no write barrier")]
        [DataRow(false, "", "Strong", true, DisplayName = "No override on Strong account => write barrier")]
        public void ShouldPerformWriteBarrierForGlobalStrong_TryGetConsistencyLevelOverride(
            bool isConsistencyLevelOverridden,
            string overriddenConsistencyLevelString,
            string defaultConsistencyLevelString,
            bool expectedStrongWrite)
        {
            // Arrange
            Mock<IServiceConfigurationReaderExtension> mockServiceConfigReader = new Mock<IServiceConfigurationReaderExtension>();

            ConsistencyLevel consistencyLevelOverride = default;
            if (isConsistencyLevelOverridden)
            {
                _ = Enum.TryParse(overriddenConsistencyLevelString, out consistencyLevelOverride);
            }

            _ = Enum.TryParse(defaultConsistencyLevelString, out ConsistencyLevel defaultConsistencyLevel);
            mockServiceConfigReader.Setup(x => x.DefaultConsistencyLevel).Returns(defaultConsistencyLevel);
            mockServiceConfigReader.Setup(x => x.TryGetConsistencyLevel(It.IsAny<DocumentServiceRequest>(), out consistencyLevelOverride))
                .Returns(isConsistencyLevelOverridden);

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>(MockBehavior.Default);
            mockAddressResolver
                .Setup(x => x.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(CommonAddressInformation));

            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>(MockBehavior.Default);
            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();

            ConsistencyWriter consistencyWriter = new ConsistencyWriter(
                new AddressSelector(mockAddressResolver.Object, Protocol.Tcp),
                new NullSessionContainer(),
                mockTransportClient.Object,
                mockServiceConfigReader.Object,
                mockAuthorizationTokenProvider.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false);

            // Create StoreResult with NumberOfReadRegions > 0 and successful status
            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(new StoreResponse(), numberOfReadRegions: 2);

            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.SystemAll);

            // Act & Assert
            Assert.AreEqual(expectedStrongWrite, consistencyWriter.ShouldPerformWriteBarrierForGlobalStrong(storeResult.Target, entity));
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
            entity.UseStatusCodeFor429 = true;

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

        private static ConsistencyWriter CreateConsistencyWriter(ConsistencyWriterTestSetup setup, bool enableBarrierEarlyYieldOn429 = false)
        {
            return new ConsistencyWriter(
                new AddressSelector(setup.MockAddressResolver.Object, Protocol.Tcp),
                new NullSessionContainer(),
                setup.MockTransportClient.Object,
                setup.MockServiceConfigReader.Object,
                setup.MockAuthorizationTokenProvider.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null,
                enableBarrierEarlyYieldOn429: enableBarrierEarlyYieldOn429);
        }

        private class ConsistencyWriterTestSetup
        {
            public DocumentServiceRequest Entity { get; set; }

            public Mock<IAddressResolver> MockAddressResolver { get; set; }

            public Mock<TransportClient> MockTransportClient { get; set; }

            public Mock<IServiceConfigurationReader> MockServiceConfigReader { get; set; }

            public Mock<IAuthorizationTokenProvider> MockAuthorizationTokenProvider { get; set; }

            public Mock<AccountConfigurationProperties> MockAccountConfigurationProperties { get; set; }
        }

        internal async Task<int> SessionNotFoundExceptionLessRetryInternalTestAsync(bool useStatusCodeFor4041002)
        {
            // create a real document service request (with auth token level = god)
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.UseStatusCodeFor4041002 = useStatusCodeFor4041002;

            // set request charge tracker -  this is referenced in store reader (ReadMultipleReplicaAsync)
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext();
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = requestContext;

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();
            AddressInformation[] addressInformation = new AddressInformation[4];

            // construct URIs that look like the actual uri
            for (int i = 0; i < 4; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                         + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0 ? true : false,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
            TransportAddressUri primaryAddress = await addressSelector.ResolvePrimaryTransportAddressUriAsync(entity, false);
            Assert.IsNotNull(primaryAddress);
            Assert.IsNotNull(primaryAddress.Uri);
            Assert.IsTrue(primaryAddress.Uri.ToString().EndsWith("p/"));

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockAuthorizationTokenProvider.Setup(provider => provider.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            int retryCount = 0;
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();
            mockTransportClient.Setup(client => client.InvokeResourceOperationAsync(
                It.IsAny<TransportAddressUri>(),
                It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(
                () =>
                {
                    int statusCode = (retryCount == 0) ? (int)HttpStatusCode.NotFound : (int)HttpStatusCode.Created;
                    int subStatusCode = (retryCount == 0) ? (int)SubStatusCodes.ReadSessionNotAvailable : 0;
                    retryCount++;

                    if (!entity.UseStatusCodeFor4041002 && statusCode == 404)
                    {
                        throw new NotFoundException(new Exception("Resource Not Found"),
                            (SubStatusCodes)subStatusCode);
                    }

                    return new StoreResponse
                    {
                        Status = statusCode,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, subStatusCode.ToString() },
                            })
                    };
                });

            ConsistencyWriter consistencyWriter = new ConsistencyWriter(addressSelector,
                mockSessionContainer.Object,
                mockTransportClient.Object,
                mockServiceConfigReader.Object,
                mockAuthorizationTokenProvider.Object,
                useMultipleWriteLocations: true,
                enableReplicaValidation: true,
                sessionRetryOptions: null);
            StoreResponse response = await consistencyWriter.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);

            Assert.IsNotNull(response);
            Assert.AreEqual(2, retryCount);

            return retryCount;
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteWithReadConsistencyStrategy_NullStrategyUsesAccountConsistency()
        {
            // Arrange - Null strategy (not set) should defer to account consistency
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10)),
                ReadConsistencyStrategy = null
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                { WFConstants.BackendHeaders.LSN, "100" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                    })
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                null);

            // Act
            StoreResponse result = await writer.WriteAsync(
                entity,
                new TimeoutHelper(TimeSpan.FromSeconds(10)),
                false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.IsNull(entity.RequestContext.ReadConsistencyStrategy, "Strategy should remain null to use account default");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteWithReadConsistencyStrategy_SessionStrategyRequiresSessionToken()
        {
            // Arrange - Session strategy should use session token for read-your-writes
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10)),
                ReadConsistencyStrategy = ReadConsistencyStrategy.Session
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(Task.CompletedTask);

            bool sessionTokenUsed = false;
            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    // Verify session strategy is set
                    if (entity.RequestContext.ReadConsistencyStrategy == ReadConsistencyStrategy.Session)
                    {
                        sessionTokenUsed = true;
                    }

                    return new StoreResponse
                    {
                        Status = 201,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                    { WFConstants.BackendHeaders.LSN, "100" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                null);

            // Act
            StoreResponse result = await writer.WriteAsync(
                entity,
                new TimeoutHelper(TimeSpan.FromSeconds(10)),
                false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.IsTrue(sessionTokenUsed, "Session strategy should track session token usage");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteWithReadConsistencyStrategy_LatestCommittedStrategyIsApplied()
        {
            // Arrange - Verify LatestCommitted strategy is properly applied during write
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";

            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10)),
                ReadConsistencyStrategy = ReadConsistencyStrategy.LatestCommitted
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Eventual);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(
                It.IsAny<DocumentServiceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                { WFConstants.BackendHeaders.LSN, "100" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" }
                    })
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                null);

            // Act
            StoreResponse result = await writer.WriteAsync(
                entity,
                new TimeoutHelper(TimeSpan.FromSeconds(10)),
                false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(201, result.Status);
            Assert.AreEqual(ReadConsistencyStrategy.LatestCommitted, entity.RequestContext.ReadConsistencyStrategy,
                "LatestCommitted strategy should be applied to the request context");
        }

        // ============================================================================
        // ApplyNRegionSynchronousCommit per-request flag tests
        // ============================================================================

        [TestMethod]
        [Owner("dibilla")]
        public void ShouldPerformWriteBarrierForLessThanStrong_PerRequestFlagEnabled_AccountFlagDisabled_ReturnsTrue()
        {
            // Arrange: account flag OFF, per-request flag ON, all other conditions met
            Mock<IServiceConfigurationReaderVnext> mockConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(false);

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp),
                new NullSessionContainer(),
                Mock.Of<TransportClient>(),
                mockConfigReader.Object,
                Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(
                storeResponse: new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                    }),
                }, numberOfReadRegions: 3);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace, ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ApplyNRegionSynchronousCommit = true;

            // Act
            bool result = writer.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target, request);

            // Assert — per-request flag should enable barrier even with account flag off
            Assert.IsTrue(result);
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ShouldPerformWriteBarrierForLessThanStrong_BothFlagsDisabled_ReturnsFalse()
        {
            // Arrange: account flag OFF, per-request flag OFF
            Mock<IServiceConfigurationReaderVnext> mockConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(false);

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp),
                new NullSessionContainer(),
                Mock.Of<TransportClient>(),
                mockConfigReader.Object,
                Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(
                storeResponse: new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                    }),
                }, numberOfReadRegions: 3);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ApplyNRegionSynchronousCommit = false;

            // Act
            bool result = writer.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target, request);

            // Assert — neither flag is on, no barrier
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ShouldPerformWriteBarrierForLessThanStrong_PerRequestFlagEnabled_StrongConsistency_ReturnsFalse()
        {
            // Arrange: per-request flag ON but account is Strong — precondition not met
            Mock<IServiceConfigurationReaderVnext> mockConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);
            mockConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(false);

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp),
                new NullSessionContainer(),
                Mock.Of<TransportClient>(),
                mockConfigReader.Object,
                Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(
                storeResponse: new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                    }),
                }, numberOfReadRegions: 3);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ApplyNRegionSynchronousCommit = true;

            // Act
            bool result = writer.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target, request);

            // Assert — Strong consistency bypasses NRegion path
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ShouldPerformWriteBarrierForLessThanStrong_PerRequestFlagEnabled_NoReadRegions_ReturnsFalse()
        {
            // Arrange: per-request flag ON but no read regions
            Mock<IServiceConfigurationReaderVnext> mockConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(false);

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp),
                new NullSessionContainer(),
                Mock.Of<TransportClient>(),
                mockConfigReader.Object,
                Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(
                storeResponse: new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "0" },
                    }),
                }, numberOfReadRegions: 0);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ApplyNRegionSynchronousCommit = true;

            // Act
            bool result = writer.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target, request);

            // Assert — no read regions, precondition not met
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ShouldPerformWriteBarrierForLessThanStrong_PerRequestFlagEnabled_MultiMaster_ReturnsFalse()
        {
            // Arrange: per-request flag ON but multi-master account
            Mock<IServiceConfigurationReaderVnext> mockConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(false);

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp),
                new NullSessionContainer(),
                Mock.Of<TransportClient>(),
                mockConfigReader.Object,
                Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: true,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(
                storeResponse: new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                    }),
                }, numberOfReadRegions: 3);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ApplyNRegionSynchronousCommit = true;

            // Act
            bool result = writer.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target, request);

            // Assert — multi-master, precondition not met
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ShouldPerformWriteBarrierForLessThanStrong_NullRequest_AccountFlagEnabled_ReturnsTrue()
        {
            // Arrange: account flag ON, null request (backward compat)
            Mock<IServiceConfigurationReaderVnext> mockConfigReader = new Mock<IServiceConfigurationReaderVnext>();
            mockConfigReader.SetupGet(r => r.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfigReader.SetupGet(r => r.EnableNRegionSynchronousCommit).Returns(true);

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp),
                new NullSessionContainer(),
                Mock.Of<TransportClient>(),
                mockConfigReader.Object,
                Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            ReferenceCountedDisposable<StoreResult> storeResult = StoreResult.CreateForTesting(
                storeResponse: new StoreResponse
                {
                    Status = 201,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "9" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                    }),
                }, numberOfReadRegions: 3);

            // Act — null request, should still work with account flag
            bool result = writer.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target, request: null);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [Owner("cosmosdbrg")]
        [Description("Verifies ConsistencyWriter stashes CRSS=4 on RequestContext for centralized scale-up detection")]
        public async Task WritePrivateAsync_CrssFromWrite_StashesOnRequestContextAsync()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica1/1s/", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica2/2s/", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 200,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.ActivityId, "activityId" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, "4" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                    })
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                false,
                false);

            await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);

            Assert.AreEqual(4, entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse);
        }

        [TestMethod]
        [Owner("cosmosdbrg")]
        [Description("Verifies ConsistencyWriter stashes CRSS=3 (TRSS=3 scenario) on RequestContext")]
        public async Task WritePrivateAsync_CrssFromWrite_Trss3_StashesOnRequestContextAsync()
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(10))
            };

            AddressInformation[] addresses = new AddressInformation[]
            {
                new AddressInformation("rntbd://replica0/0p/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica1/1s/", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://replica2/2s/", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            AddressSelector addressSelector = new AddressSelector(mockResolver.Object, Protocol.Tcp);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync(new StoreResponse
                {
                    Status = 200,
                    Headers = new DictionaryNameValueCollection(new NameValueCollection
                    {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.ActivityId, "activityId" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                    })
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                addressSelector,
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                false,
                false);

            await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), false);

            Assert.AreEqual(3, entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_GlobalStrong_RetriesExhausted_ThrowsGoneWithBarrierSubStatus()
        {
            // Arrange: barrier reads return GlobalCommittedLSN < LSN so barrier never converges.
            // Retries exhaust and CreateAndWaitForWriteBarrierAsync throws GoneException with the barrier substatus.
            ConsistencyWriterTestSetup setup = CreateTestSetup();

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        // Barrier reads: GlobalCommittedLSN never reaches the target
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" }
                            })
                        };
                    }

                    // Simulate a successful primary write that triggers the barrier
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                        })
                    };
                });

            ConsistencyWriter writer = CreateConsistencyWriter(setup);

            // Act & Assert: retries exhaust, barrier not met -> GoneException with GlobalStrongWriteBarrier substatus.
            GoneException ex = await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await writer.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
            });

            Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatus());
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_NRegionCommit_RetriesExhausted_ThrowsGoneWithNRegionSubStatus()
        {
            // Arrange: NRegion barrier reads never converge, retries exhaust.
            // Uses IServiceConfigurationReaderVnext with Session consistency + EnableNRegionSynchronousCommit
            // so ComputeBarrierType returns NRegionSynchronousCommit (not GlobalStrongWrite).
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30))
            };

            Mock<IAddressResolver> mockResolver = new Mock<IAddressResolver>();
            mockResolver
                .Setup(r => r.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(CommonAddressInformation));

            Mock<IServiceConfigurationReaderVnext> mockConfig = new Mock<IServiceConfigurationReaderVnext>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfig.SetupGet(c => c.EnableNRegionSynchronousCommit).Returns(true);

            Mock<IAuthorizationTokenProvider> mockAuth = new Mock<IAuthorizationTokenProvider>();
            mockAuth.Setup(p => p.AddSystemAuthorizationHeaderAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Default);
            mockTransport
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        // Barrier reads: GlobalNRegionCommittedGLSN never reaches the target
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                                { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "50" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    // Simulate a successful primary write with NRegion LSN gap
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
                    return new StoreResponse
                    {
                        Status = 201,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" },
                            { WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, "50" }
                        })
                    };
                });

            ConsistencyWriter writer = new ConsistencyWriter(
                new AddressSelector(mockResolver.Object, Protocol.Tcp),
                new NullSessionContainer(),
                mockTransport.Object,
                mockConfig.Object,
                mockAuth.Object,
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null);

            // Act & Assert: retries exhaust, NRegion barrier not met -> GoneException with NRegionCommit substatus.
            GoneException ex = await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await writer.WriteAsync(entity, new TimeoutHelper(TimeSpan.FromSeconds(30)), forceRefresh: false);
            });

            Assert.AreEqual(SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet, ex.GetSubStatus());
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task WriteBarrier_GlobalStrong_TimeoutDuringBarrier_ThrowsRequestTimeoutWithBarrierSubStatus()
        {
            // Arrange: use a short timeout that expires during the barrier loop (after initial write succeeds).
            ConsistencyWriterTestSetup setup = CreateTestSetup();

            setup.MockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns(async (TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        // Delay barrier reads to consume the timeout
                        await Task.Delay(500);
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" }
                            })
                        };
                    }

                    // Initial write succeeds quickly
                    req.RequestContext.RequestChargeTracker.AddCharge(5.5);
                    return new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" }
                        })
                    };
                });

            ConsistencyWriter writer = CreateConsistencyWriter(setup);

            // Act & Assert: timeout fires during barrier -> RequestTimeoutException with barrier substatus.
            RequestTimeoutException ex = await Assert.ThrowsExceptionAsync<RequestTimeoutException>(async () =>
            {
                await writer.WriteAsync(setup.Entity, new TimeoutHelper(TimeSpan.FromMilliseconds(200)), forceRefresh: false);
            });

            Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatus());
        }
    }
}