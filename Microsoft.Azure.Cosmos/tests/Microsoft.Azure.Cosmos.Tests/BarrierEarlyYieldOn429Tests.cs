//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Resources;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public sealed class BarrierEarlyYieldOn429Tests
    {
        private static readonly Uri Location1Endpoint = new Uri("https://location1.documents.azure.com");
        private static readonly Uri Location2Endpoint = new Uri("https://location2.documents.azure.com");
        private static readonly PartitionKeyRange TestPartitionKeyRange = new PartitionKeyRange()
        {
            Id = "0",
            MinInclusive = "3F-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF",
            MaxExclusive = "5F-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF",
        };

        [TestMethod]
        public async Task RequestTimeoutWithWriteBarrierThrottledSubStatusDoesNotMarkEndpointUnavailable()
        {
            using GlobalEndpointManager endpointManager = CreateGlobalEndpointManager(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                includePreferredLocations: true);
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = CreatePartitionKeyRangeLocationCache(endpointManager);
            DocumentServiceRequest request = CreateWriteRequest();
            request.RequestContext.ResolvedPartitionKeyRange = TestPartitionKeyRange;

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery: true,
                isThinClientEnabled: false);

            retryPolicy.OnBeforeSendRequest(request);

            DocumentClientException requestTimeoutException = CreateDocumentClientException(
                request,
                HttpStatusCode.RequestTimeout,
                SubStatusCodes.Server_WriteBarrierThrottled);

            await retryPolicy.ShouldRetryAsync(requestTimeoutException, CancellationToken.None);

            GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo failoverInfo = GetPartitionKeyRangeFailoverInfo(
                partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: false);

            Assert.IsNull(failoverInfo);
        }

        [TestMethod]
        public async Task RequestTimeoutWithWriteBarrierThrottledSubStatusReturnsNoRetry()
        {
            using GlobalEndpointManager endpointManager = CreateGlobalEndpointManager(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                includePreferredLocations: true);
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = CreatePartitionKeyRangeLocationCache(endpointManager);
            DocumentServiceRequest request = CreateWriteRequest();
            request.RequestContext.ResolvedPartitionKeyRange = TestPartitionKeyRange;

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery: true,
                isThinClientEnabled: false);

            retryPolicy.OnBeforeSendRequest(request);

            DocumentClientException requestTimeoutException = CreateDocumentClientException(
                request,
                HttpStatusCode.RequestTimeout,
                SubStatusCodes.Server_WriteBarrierThrottled);

            ShouldRetryResult result = await retryPolicy.ShouldRetryAsync(requestTimeoutException, CancellationToken.None);

            // The synthetic 408/21013 is NOT retried by the SDK — Direct handles retries
            // internally. The SDK only ensures no unnecessary cross-region failover.
            Assert.IsFalse(result.ShouldRetry);

            GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo failoverInfo = GetPartitionKeyRangeFailoverInfo(
                partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: false);

            Assert.IsNull(failoverInfo);
        }

        [TestMethod]
        public async Task RequestTimeoutWithoutSubStatusMarksEndpointUnavailable()
        {
            using GlobalEndpointManager endpointManager = CreateGlobalEndpointManager(
                useMultipleWriteLocations: false,
                enableEndpointDiscovery: true,
                includePreferredLocations: true);
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = CreatePartitionKeyRangeLocationCache(endpointManager);
            DocumentServiceRequest request = CreateWriteRequest();
            request.RequestContext.ResolvedPartitionKeyRange = TestPartitionKeyRange;

            ClientRetryPolicy retryPolicy = new ClientRetryPolicy(
                endpointManager,
                partitionKeyRangeLocationCache,
                new RetryOptions(),
                enableEndpointDiscovery: true,
                isThinClientEnabled: false);

            retryPolicy.OnBeforeSendRequest(request);

            DocumentClientException requestTimeoutException = CreateDocumentClientException(
                request,
                HttpStatusCode.RequestTimeout,
                SubStatusCodes.Unknown);

            await retryPolicy.ShouldRetryAsync(requestTimeoutException, CancellationToken.None);

            GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo failoverInfo = GetPartitionKeyRangeFailoverInfo(
                partitionKeyRangeLocationCache,
                request.RequestContext.ResolvedPartitionKeyRange,
                isReadOnlyOrMultiMasterWriteRequest: false);

            Assert.IsNotNull(failoverInfo);
            Assert.AreEqual(endpointManager.ReadEndpoints[1], failoverInfo.Current);
        }

        [TestMethod]
        public void CosmosClientOptionsDefaultsEnableBarrierEarlyYieldToTrue()
        {
            CosmosClientOptions options = new CosmosClientOptions();

            Assert.IsTrue(options.EnableBarrierEarlyYieldOn429);
        }

        [TestMethod]
        public void CosmosClientOptionsDisableBarrierEarlyYield()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                EnableBarrierEarlyYieldOn429 = false,
            };

            Assert.IsFalse(options.EnableBarrierEarlyYieldOn429);
        }

        [TestMethod]
        public void ConnectionPolicyDefaultsEnableBarrierEarlyYieldToFalse()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();

            Assert.IsFalse(connectionPolicy.EnableBarrierEarlyYieldOn429);
        }

        [TestMethod]
        public void CosmosClientOptionsFlowsEnableBarrierEarlyYieldToConnectionPolicy()
        {
            foreach (bool enableBarrierEarlyYieldOn429 in new[] { false, true })
            {
                CosmosClientOptions options = new CosmosClientOptions
                {
                    EnableBarrierEarlyYieldOn429 = enableBarrierEarlyYieldOn429,
                };

                ConnectionPolicy connectionPolicy = InvokeGetConnectionPolicy(options, clientId: 0);

                Assert.AreEqual(
                    enableBarrierEarlyYieldOn429,
                    connectionPolicy.EnableBarrierEarlyYieldOn429);
            }
        }

        [TestMethod]
        public async Task EndToEndConstructorChainWriteBarrierEarlyYieldThroughConsistencyWriter()
        {
            // True E2E for the new client option flow:
            // CosmosClientOptions.EnableBarrierEarlyYieldOn429 → ConnectionPolicy →
            // DocumentClient/StoreClientFactory constructor chain → ConsistencyWriter field.
            //
            // Transport mock returns:
            //   1st call: primary write succeeds (201) with GlobalCommittedLSN < LSN → barrier needed
            //   Subsequent calls: all 429s → barrier cannot be satisfied → early yield fires
            int callCount = 0;
            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .Returns(() =>
                {
                    int call = Interlocked.Increment(ref callCount);
                    if (call == 1)
                    {
                        // Primary write succeeds, but GlobalCommittedLSN < LSN triggers barrier
                        return Task.FromResult(new StoreResponse
                        {
                            Status = (int)HttpStatusCode.Created,
                            Headers = new RequestNameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                                { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() },
                                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                                { WFConstants.BackendHeaders.CurrentWriteQuorum, "2" },
                            },
                        });
                    }

                    // Barrier requests all return 429
                    return Task.FromResult(new StoreResponse
                    {
                        Status = (int)HttpStatusCode.TooManyRequests,
                        Headers = new RequestNameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "50" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                            { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() },
                        },
                    });
                });

            // Constructor flag = true (new SDK path), Strong consistency → barrier will trigger.
            Mock<IServiceConfigurationReader> mockServiceConfig = new Mock<IServiceConfigurationReader>();
            mockServiceConfig.SetupGet(x => x.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Strong);

            ConsistencyWriter writer = CreateConsistencyWriterForBarrierTest(
                enableBarrierEarlyYieldOn429: true,
                transportClient: mockTransport.Object,
                addressSelector: CreateAddressSelectorWithReplicas(),
                serviceConfigReader: mockServiceConfig.Object);

            EnsureRmResourcesLoaded();
            DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Create,
                "dbs/testdb/colls/testcol/docs/testdoc",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ResolvedPartitionKeyRange = TestPartitionKeyRange;
            request.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30));

            // WriteAsync → WritePrivateAsync → CreateAndWaitForWriteBarrierAsync
            // The constructor-provided field is now the only early-yield switch.
            // It should trigger RequestTimeoutException with substatus 21013.
            RequestTimeoutException exception = await Assert.ThrowsExceptionAsync<RequestTimeoutException>(
                () => writer.WriteAsync(
                    request,
                    new TimeoutHelper(TimeSpan.FromSeconds(30)),
                    forceRefresh: false));

            Assert.AreEqual(
                SubStatusCodes.Server_WriteBarrierThrottled,
                exception.GetSubStatus(),
                "WriteAsync must throw RequestTimeoutException/21013 when the constructor flag enables early yield and all barrier replicas return 429.");
        }

        [TestMethod]
        public async Task EndToEndHeaderToWriteBarrierNoEarlyYieldWhenFlagAbsent()
        {
            // Negative E2E for the constructor-chain flow: the writer is created with
            // early yield disabled, so the barrier spins normally and returns
            // GoneException instead of RequestTimeoutException/21013.
            int callCount = 0;
            Mock<TransportClient> mockTransport = new Mock<TransportClient>();
            mockTransport
                .Setup(t => t.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .Returns(() =>
                {
                    int call = Interlocked.Increment(ref callCount);
                    if (call == 1)
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = (int)HttpStatusCode.Created,
                            Headers = new RequestNameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                                { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() },
                                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                                { WFConstants.BackendHeaders.CurrentWriteQuorum, "2" },
                            },
                        });
                    }

                    return Task.FromResult(new StoreResponse
                    {
                        Status = (int)HttpStatusCode.TooManyRequests,
                        Headers = new RequestNameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "50" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                            { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() },
                        },
                    });
                });

            // Both flags false, Strong consistency → barrier triggers but no early yield
            Mock<IServiceConfigurationReader> mockServiceConfig = new Mock<IServiceConfigurationReader>();
            mockServiceConfig.SetupGet(x => x.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Strong);

            ConsistencyWriter writer = CreateConsistencyWriterForBarrierTest(
                enableBarrierEarlyYieldOn429: false,
                transportClient: mockTransport.Object,
                addressSelector: CreateAddressSelectorWithReplicas(),
                serviceConfigReader: mockServiceConfig.Object);

            EnsureRmResourcesLoaded();
            DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Create,
                "dbs/testdb/colls/testcol/docs/testdoc",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ResolvedPartitionKeyRange = TestPartitionKeyRange;
            request.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30));
            // NOTE: The writer was constructed with early yield disabled.

            // Should throw GoneException (barrier not met) instead of RequestTimeoutException/21013
            GoneException exception = await Assert.ThrowsExceptionAsync<GoneException>(
                () => writer.WriteAsync(
                    request,
                    new TimeoutHelper(TimeSpan.FromSeconds(30)),
                    forceRefresh: false));

            Assert.AreEqual(
                SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet,
                exception.GetSubStatus(),
                "Without the early yield flag, barrier failure should throw GoneException, not RequestTimeoutException/21013.");
        }

        [TestMethod]
        public async Task WriteBarrierThrowsTimeoutWhenAllReplicas429AndEarlyYieldEnabled()
        {
            Mock<TransportClient> transportClient = CreateTransportClientMock(
                Enumerable.Repeat(
                    CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 50, globalCommittedLsn: 50),
                    40));

            ConsistencyWriter writer = CreateConsistencyWriterForBarrierTest(
                enableBarrierEarlyYieldOn429: true,
                transportClient: transportClient.Object,
                addressSelector: CreateAddressSelectorWithReplicas());
            DocumentServiceRequest barrierRequest = CreateBarrierRequest();

            RequestTimeoutException exception = await Assert.ThrowsExceptionAsync<RequestTimeoutException>(
                () => InvokeWaitForWriteBarrierAsync(
                    writer,
                    barrierRequest,
                    selectedGlobalCommittedLsn: 100,
                    lsnAttributeSelector: sr => sr.GlobalCommittedLSN));

            Assert.AreEqual(SubStatusCodes.Server_WriteBarrierThrottled, exception.GetSubStatus());
        }

        [TestMethod]
        public async Task WriteBarrierReturnsFalseWhenAllReplicas429AndEarlyYieldDisabled()
        {
            Mock<TransportClient> transportClient = CreateTransportClientMock(
                Enumerable.Repeat(
                    CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 50, globalCommittedLsn: 50),
                    40));

            ConsistencyWriter writer = CreateConsistencyWriterForBarrierTest(
                enableBarrierEarlyYieldOn429: false,
                transportClient: transportClient.Object,
                addressSelector: CreateAddressSelectorWithReplicas());
            DocumentServiceRequest barrierRequest = CreateBarrierRequest();

            bool result = await InvokeWaitForWriteBarrierAsync(
                writer,
                barrierRequest,
                selectedGlobalCommittedLsn: 100,
                lsnAttributeSelector: sr => sr.GlobalCommittedLSN);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task WriteBarrierSucceedsWhenBarrierConditionMetDespite429s()
        {
            foreach (bool enableEarlyYield in new[] { false, true })
            {
                Mock<TransportClient> transportClient = CreateTransportClientMock(new[]
                {
                    CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 50, globalCommittedLsn: 50),
                    CreateStoreResponse(HttpStatusCode.OK, lsn: 100, globalCommittedLsn: 100),
                });

                ConsistencyWriter writer = CreateConsistencyWriterForBarrierTest(
                    enableBarrierEarlyYieldOn429: enableEarlyYield,
                    transportClient: transportClient.Object,
                    addressSelector: CreateAddressSelectorWithReplicas());
                DocumentServiceRequest barrierRequest = CreateBarrierRequest();

                bool result = await InvokeWaitForWriteBarrierAsync(
                    writer,
                    barrierRequest,
                    selectedGlobalCommittedLsn: 100,
                    lsnAttributeSelector: sr => sr.GlobalCommittedLSN);

                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public async Task ReadBarrierReturnsThrottledResponseWhenAllReplicas429AndEarlyYieldEnabled()
        {
            AddressSelector addressSelector = CreateAddressSelectorWithReplicas(replicaCount: 2);
            Mock<TransportClient> transportClient = CreateTransportClientMock(new[]
            {
                CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 50, globalCommittedLsn: 50),
                CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 50, globalCommittedLsn: 50),
            });

            QuorumReader reader = CreateQuorumReaderForBarrierTest(
                enableBarrierEarlyYieldOn429: true,
                storeReader: CreateStoreReader(transportClient.Object, addressSelector),
                addressSelector: addressSelector);
            DocumentServiceRequest barrierRequest = CreateBarrierRequest();

            (bool isSuccess, StoreResponse throttledResponse) result = await InvokeWaitForReadBarrierAsync(
                reader,
                barrierRequest,
                allowPrimary: true,
                readQuorum: 2,
                readBarrierLsn: 100,
                targetGlobalCommittedLsn: 0,
                readMode: ReadMode.Strong);

            Assert.IsFalse(result.isSuccess);
            Assert.IsNotNull(result.throttledResponse);
            Assert.AreEqual(HttpStatusCode.TooManyRequests, result.throttledResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReadBarrierReturnsFalseWhenAllReplicas429AndEarlyYieldDisabled()
        {
            AddressSelector addressSelector = CreateAddressSelectorWithReplicas(replicaCount: 2);
            Mock<TransportClient> transportClient = CreateTransportClientMock(
                Enumerable.Repeat(
                    CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 50, globalCommittedLsn: 50),
                    20));

            QuorumReader reader = CreateQuorumReaderForBarrierTest(
                enableBarrierEarlyYieldOn429: false,
                storeReader: CreateStoreReader(transportClient.Object, addressSelector),
                addressSelector: addressSelector);
            DocumentServiceRequest barrierRequest = CreateBarrierRequest();

            (bool isSuccess, StoreResponse throttledResponse) result = await InvokeWaitForReadBarrierAsync(
                reader,
                barrierRequest,
                allowPrimary: true,
                readQuorum: 2,
                readBarrierLsn: 100,
                targetGlobalCommittedLsn: 0,
                readMode: ReadMode.Strong);

            Assert.IsFalse(result.isSuccess);
            Assert.IsNull(result.throttledResponse);
        }

        [TestMethod]
        public async Task ReadBarrierSucceedsWhenBarrierConditionMetDespite429s()
        {
            foreach (bool enableEarlyYield in new[] { false, true })
            {
                AddressSelector addressSelector = CreateAddressSelectorWithReplicas(replicaCount: 2);
                Mock<TransportClient> transportClient = CreateTransportClientMock(new[]
                {
                    CreateStoreResponse(HttpStatusCode.TooManyRequests, lsn: 100, globalCommittedLsn: 100),
                    CreateStoreResponse(HttpStatusCode.OK, lsn: 100, globalCommittedLsn: 100),
                });

                QuorumReader reader = CreateQuorumReaderForBarrierTest(
                    enableBarrierEarlyYieldOn429: enableEarlyYield,
                    storeReader: CreateStoreReader(transportClient.Object, addressSelector),
                    addressSelector: addressSelector);
                DocumentServiceRequest barrierRequest = CreateBarrierRequest();

                (bool isSuccess, StoreResponse throttledResponse) result = await InvokeWaitForReadBarrierAsync(
                    reader,
                    barrierRequest,
                    allowPrimary: true,
                    readQuorum: 2,
                    readBarrierLsn: 100,
                    targetGlobalCommittedLsn: 100,
                    readMode: ReadMode.Strong);

                Assert.IsTrue(result.isSuccess);
                Assert.IsNull(result.throttledResponse);
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void FullConstructorChainPropagatesEarlyYieldFlagToConsistencyWriterAndQuorumReader(bool enableFlag)
        {
            // True E2E constructor chain validation:
            // CosmosClientOptions → ConnectionPolicy → StoreClientFactory → StoreClient
            //   → ReplicatedResourceClient → ConsistencyWriter.enableBarrierEarlyYieldOn429
            //                               → ConsistencyReader → QuorumReader.enableBarrierEarlyYieldOn429

            // Step 1: CosmosClientOptions → ConnectionPolicy
            CosmosClientOptions options = new CosmosClientOptions
            {
                EnableBarrierEarlyYieldOn429 = enableFlag,
            };
            ConnectionPolicy connectionPolicy = InvokeGetConnectionPolicy(options, clientId: 0);
            Assert.AreEqual(enableFlag, connectionPolicy.EnableBarrierEarlyYieldOn429,
                "ConnectionPolicy must reflect CosmosClientOptions value.");

            // Step 2: ConnectionPolicy → StoreClientFactory (same as DocumentClient does)
            using StoreClientFactory factory = new StoreClientFactory(
                protocol: Protocol.Tcp,
                requestTimeoutInSeconds: 60,
                maxConcurrentConnectionOpenRequests: 1,
                enableBarrierEarlyYieldOn429: connectionPolicy.EnableBarrierEarlyYieldOn429);

            // Step 3: StoreClientFactory.CreateStoreClient → StoreClient → ReplicatedResourceClient
            StoreClient storeClient = factory.CreateStoreClient(
                addressResolver: Mock.Of<IAddressResolver>(),
                sessionContainer: Mock.Of<ISessionContainer>(),
                serviceConfigurationReader: Mock.Of<IServiceConfigurationReader>(),
                authorizationTokenProvider: Mock.Of<IAuthorizationTokenProvider>(),
                useFallbackClient: false);

            // Step 4: Drill into StoreClient → replicatedResourceClient → consistencyWriter
            FieldInfo replicatedResourceClientField = typeof(StoreClient).GetField(
                "replicatedResourceClient",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(replicatedResourceClientField, "StoreClient must have replicatedResourceClient field.");
            object replicatedResourceClient = replicatedResourceClientField.GetValue(storeClient);
            Assert.IsNotNull(replicatedResourceClient);

            // Step 5a: ReplicatedResourceClient → consistencyWriter → enableBarrierEarlyYieldOn429
            FieldInfo consistencyWriterField = typeof(ReplicatedResourceClient).GetField(
                "consistencyWriter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(consistencyWriterField, "ReplicatedResourceClient must have consistencyWriter field.");
            object consistencyWriter = consistencyWriterField.GetValue(replicatedResourceClient);
            Assert.IsNotNull(consistencyWriter);

            FieldInfo writerFlagField = typeof(ConsistencyWriter).GetField(
                "enableBarrierEarlyYieldOn429",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(writerFlagField);
            Assert.AreEqual(enableFlag, (bool)writerFlagField.GetValue(consistencyWriter),
                $"ConsistencyWriter.enableBarrierEarlyYieldOn429 must be {enableFlag} through the full constructor chain.");

            // Step 5b: ReplicatedResourceClient → consistencyReader → quorumReader → enableBarrierEarlyYieldOn429
            FieldInfo consistencyReaderField = typeof(ReplicatedResourceClient).GetField(
                "consistencyReader",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(consistencyReaderField, "ReplicatedResourceClient must have consistencyReader field.");
            object consistencyReader = consistencyReaderField.GetValue(replicatedResourceClient);
            Assert.IsNotNull(consistencyReader);

            FieldInfo quorumReaderField = typeof(ConsistencyReader).GetField(
                "quorumReader",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(quorumReaderField, "ConsistencyReader must have quorumReader field.");
            object quorumReader = quorumReaderField.GetValue(consistencyReader);
            Assert.IsNotNull(quorumReader);

            FieldInfo readerFlagField = typeof(QuorumReader).GetField(
                "enableBarrierEarlyYieldOn429",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(readerFlagField);
            Assert.AreEqual(enableFlag, (bool)readerFlagField.GetValue(quorumReader),
                $"QuorumReader.enableBarrierEarlyYieldOn429 must be {enableFlag} through the full constructor chain.");
        }

        private static DocumentClientException CreateDocumentClientException(
            DocumentServiceRequest request,
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode)
        {
            return new DocumentClientException(
                message: "Simulated request timeout.",
                innerException: new Exception("inner"),
                statusCode: statusCode,
                substatusCode: subStatusCode,
                requestUri: request.RequestContext.LocationEndpointToRoute,
                responseHeaders: new DictionaryNameValueCollection());
        }

        private static DocumentServiceRequest CreateWriteRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
        }

        private static Mock<IAddressResolver> CreateMockAddressResolver()
        {
            AddressInformation[] addresses = Enumerable.Range(0, 3)
                .Select(index => new AddressInformation(
                    physicalUri: $"rntbd://host:14003/apps/00000000-0000-0000-0000-000000000001/services/00000000-0000-0000-0000-000000000002/partitions/00000000-0000-0000-0000-000000000003/replicas/{index}{(index == 0 ? "p" : "s")}/",
                    isPublic: false,
                    isPrimary: index == 0,
                    protocol: Protocol.Tcp))
                .ToArray();

            Mock<IAddressResolver> resolver = new Mock<IAddressResolver>();
            resolver
                .Setup(r => r.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<DocumentServiceRequest, bool, CancellationToken>((req, _, _) =>
                    req.RequestContext.ResolvedPartitionKeyRange ??= TestPartitionKeyRange)
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            return resolver;
        }

        private static DocumentServiceRequest CreateBarrierRequest()
        {
            EnsureRmResourcesLoaded();
            DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Head,
                "dbs/testdb/colls/testcol",
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30));
            request.RequestContext.RequestChargeTracker = new RequestChargeTracker();
            request.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            request.RequestContext.RouteToLocation(Location1Endpoint);
            request.RequestContext.GlobalStrongWriteEndpoint = Location1Endpoint;
            return request;
        }

        private static void EnsureRmResourcesLoaded()
        {
            FieldInfo resourceManagerField = typeof(RMResources).GetField(
                "resourceMan",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(resourceManagerField);

            resourceManagerField.SetValue(
                null,
                new ResourceManager("Microsoft.Azure.Documents.RMResources", typeof(RMResources).Assembly));
        }

        private static ConsistencyWriter CreateConsistencyWriterForBarrierTest(
            bool enableBarrierEarlyYieldOn429,
            TransportClient transportClient,
            AddressSelector addressSelector,
            IServiceConfigurationReader serviceConfigReader = null)
        {
            return new ConsistencyWriter(
                addressSelector: addressSelector,
                sessionContainer: Mock.Of<ISessionContainer>(),
                transportClient: transportClient,
                serviceConfigReader: serviceConfigReader ?? Mock.Of<IServiceConfigurationReader>(),
                authorizationTokenProvider: Mock.Of<IAuthorizationTokenProvider>(),
                useMultipleWriteLocations: false,
                enableReplicaValidation: false,
                sessionRetryOptions: null,
                enableBarrierEarlyYieldOn429: enableBarrierEarlyYieldOn429);
        }

        private static QuorumReader CreateQuorumReaderForBarrierTest(
            bool enableBarrierEarlyYieldOn429,
            StoreReader storeReader,
            AddressSelector addressSelector = null,
            TransportClient transportClient = null)
        {
            addressSelector ??= new AddressSelector(Mock.Of<IAddressResolver>(), Protocol.Tcp);
            transportClient ??= Mock.Of<TransportClient>();

            return new QuorumReader(
                transportClient,
                addressSelector,
                storeReader,
                Mock.Of<IServiceConfigurationReader>(),
                Mock.Of<IAuthorizationTokenProvider>(),
                enableBarrierEarlyYieldOn429: enableBarrierEarlyYieldOn429);
        }

        private static StoreReader CreateStoreReader(
            TransportClient transportClient,
            AddressSelector addressSelector)
        {
            return new StoreReader(
                transportClient,
                addressSelector,
                new AddressEnumerator(),
                Mock.Of<ISessionContainer>(),
                enableReplicaValidation: false);
        }

        private static AddressSelector CreateAddressSelectorWithReplicas(int replicaCount = 3)
        {
            AddressInformation[] addresses = Enumerable.Range(0, replicaCount)
                .Select(index => new AddressInformation(
                    physicalUri: $"rntbd://host:14003/apps/00000000-0000-0000-0000-000000000001/services/00000000-0000-0000-0000-000000000002/partitions/00000000-0000-0000-0000-000000000003/replicas/{index}{(index == 0 ? "p" : "s")}/",
                    isPublic: false,
                    isPrimary: index == 0,
                    protocol: Protocol.Tcp))
                .ToArray();

            Mock<IAddressResolver> resolver = new Mock<IAddressResolver>();
            resolver
                .Setup(r => r.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addresses));

            return new AddressSelector(resolver.Object, Protocol.Tcp);
        }

        private static Mock<TransportClient> CreateTransportClientMock(IEnumerable<StoreResponse> responses)
        {
            ConcurrentQueue<StoreResponse> responseQueue = new ConcurrentQueue<StoreResponse>(responses);
            Mock<TransportClient> transportClient = new Mock<TransportClient>();

            Task<StoreResponse> NextResponseAsync()
            {
                if (!responseQueue.TryDequeue(out StoreResponse response))
                {
                    throw new AssertFailedException("No mocked StoreResponse available for the barrier request.");
                }

                return Task.FromResult(response);
            }

            transportClient
                .Setup(client => client.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .Returns(() => NextResponseAsync());

            transportClient
                .Setup(client => client.InvokeResourceOperationAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<DocumentServiceRequest>()))
                .Returns(() => NextResponseAsync());

            return transportClient;
        }

        private static StoreResponse CreateStoreResponse(
            HttpStatusCode statusCode,
            long lsn,
            long globalCommittedLsn)
        {
            return new StoreResponse
            {
                Status = (int)statusCode,
                Headers = new RequestNameValueCollection
                {
                    { WFConstants.BackendHeaders.LSN, lsn.ToString(CultureInfo.InvariantCulture) },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, globalCommittedLsn.ToString(CultureInfo.InvariantCulture) },
                    { WFConstants.BackendHeaders.PartitionKeyRangeId, TestPartitionKeyRange.Id },
                    { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() },
                },
            };
        }

        private static async Task<bool> InvokeWaitForWriteBarrierAsync(
            ConsistencyWriter writer,
            DocumentServiceRequest barrierRequest,
            long selectedGlobalCommittedLsn,
            Func<StoreResult, long> lsnAttributeSelector)
        {
            MethodInfo method = typeof(ConsistencyWriter).GetMethod(
                "WaitForWriteBarrierAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method);

            Task<bool> task = (Task<bool>)method.Invoke(
                writer,
                new object[] { barrierRequest, selectedGlobalCommittedLsn, lsnAttributeSelector, BarrierType.GlobalStrongWrite });

            return await task;
        }

        private static async Task<(bool isSuccess, StoreResponse throttledResponse)> InvokeWaitForReadBarrierAsync(
            QuorumReader reader,
            DocumentServiceRequest barrierRequest,
            bool allowPrimary,
            int readQuorum,
            long readBarrierLsn,
            long targetGlobalCommittedLsn,
            ReadMode readMode)
        {
            MethodInfo method = typeof(QuorumReader).GetMethod(
                "WaitForReadBarrierAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method);

            Task<ValueTuple<bool, StoreResponse>> task =
                (Task<ValueTuple<bool, StoreResponse>>)method.Invoke(
                    reader,
                    new object[]
                    {
                        barrierRequest,
                        allowPrimary,
                        readQuorum,
                        readBarrierLsn,
                        targetGlobalCommittedLsn,
                        readMode,
                    });

            return await task;
        }

        private static ConnectionPolicy InvokeGetConnectionPolicy(
            CosmosClientOptions options,
            int clientId)
        {
            MethodInfo method = typeof(CosmosClientOptions).GetMethod(
                "GetConnectionPolicy",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method);

            return (ConnectionPolicy)method.Invoke(options, new object[] { clientId });
        }

        private static DocumentServiceRequest CreateNamedDocumentRequest()
        {
            return DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                "dbs/testdb/colls/testcol",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
        }

        private static GlobalPartitionEndpointManager CreatePartitionKeyRangeLocationCache(
            GlobalEndpointManager endpointManager)
        {
            return new GlobalPartitionEndpointManagerCore(
                globalEndpointManager: endpointManager,
                isPartitionLevelFailoverEnabled: true,
                isPartitionLevelCircuitBreakerEnabled: true);
        }

        private static GlobalEndpointManager CreateGlobalEndpointManager(
            bool useMultipleWriteLocations,
            bool enableEndpointDiscovery,
            bool includePreferredLocations)
        {
            AccountProperties databaseAccount = CreateDatabaseAccount(useMultipleWriteLocations);
            ReadOnlyCollection<string> preferredLocations = includePreferredLocations
                ? new List<string>() { "location1", "location2" }.AsReadOnly()
                : new List<string>().AsReadOnly();

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(Location1Endpoint);
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = enableEndpointDiscovery,
                UseMultipleWriteLocations = useMultipleWriteLocations,
            };

            foreach (string preferredLocation in preferredLocations)
            {
                connectionPolicy.PreferredLocations.Add(preferredLocation);
            }

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockedClient.Object, connectionPolicy);
            endpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);
            return endpointManager;
        }

        private static AccountProperties CreateDatabaseAccount(bool useMultipleWriteLocations)
        {
            Collection<AccountRegion> writeLocations = useMultipleWriteLocations
                ? new Collection<AccountRegion>()
                {
                    new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() },
                    new AccountRegion() { Name = "location2", Endpoint = Location2Endpoint.ToString() },
                }
                : new Collection<AccountRegion>()
                {
                    new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() },
                };

            return new AccountProperties()
            {
                EnableMultipleWriteLocations = useMultipleWriteLocations,
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    new AccountRegion() { Name = "location1", Endpoint = Location1Endpoint.ToString() },
                    new AccountRegion() { Name = "location2", Endpoint = Location2Endpoint.ToString() },
                },
                WriteLocationsInternal = writeLocations,
            };
        }

        private static GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo GetPartitionKeyRangeFailoverInfo(
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            PartitionKeyRange partitionKeyRange,
            bool isReadOnlyOrMultiMasterWriteRequest)
        {
            string fieldName = isReadOnlyOrMultiMasterWriteRequest
                ? "PartitionKeyRangeToLocationForReadAndWrite"
                : "PartitionKeyRangeToLocationForWrite";

            FieldInfo fieldInfo = globalPartitionEndpointManager
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(fieldInfo);

            Lazy<ConcurrentDictionary<PartitionKeyRange, GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo>> partitionKeyRangeToLocation =
                (Lazy<ConcurrentDictionary<PartitionKeyRange, GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo>>)fieldInfo.GetValue(globalPartitionEndpointManager);

            partitionKeyRangeToLocation.Value.TryGetValue(
                partitionKeyRange,
                out GlobalPartitionEndpointManagerCore.PartitionKeyRangeFailoverInfo partitionKeyRangeFailoverInfo);

            return partitionKeyRangeFailoverInfo;
        }
    }
}
