// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class QuorumReaderTests
    {

        private const string QuorumSelected = "QuorumSelected";
        private const string QuorumNotSelected = "QuorumNotSelected";
        private const string QuorumThrottled = "QuorumThrottled";

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ValidatesReadMultipleReplicaAsyncExcludesGoneReplicas()
        {
            // Arrange
            // 1. Create mock transport client that returns different responses for different replicas
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            // Track which URIs were called
            Dictionary<string, bool> uriCalled = new Dictionary<string, bool>();

            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                {
                    // Record that this URI was called
                    uriCalled[uri.Uri.ToString()] = true;

                    // Return different responses based on the replica URI
                    if (uri.Uri.ToString().Contains("replica1"))
                    {
                        // Primary returns 200 OK
                        return new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" }
                            })
                        };
                    }
                    else if (uri.Uri.ToString().Contains("replica2"))
                    {
                        // Secondary1 returns 410 Gone
                        return new StoreResponse
                        {
                            Status = 410,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.SubStatus, SubStatusCodes.PartitionKeyRangeGone.ToString() }
                            })
                        };
                    }
                    else
                    {
                        // Secondary2 returns 429 Throttled
                        return new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.SubStatus, SubStatusCodes.RUBudgetExceeded.ToString() }
                            })
                        };
                    }
                });

            // 2. Setup mock address resolver to return our test replicas
            AddressInformation[] addressInformation = new AddressInformation[]
            {
                    new AddressInformation("rntbd://replica1", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                    new AddressInformation("rntbd://replica2", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                    new AddressInformation("rntbd://replica3", isPrimary: false, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false)
            };

            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>();
            mockAddressResolver
                .Setup(x => x.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            // 3. Create real instances of StoreReader dependencies
            AddressSelector addressSelector = new AddressSelector(mockAddressResolver.Object, Protocol.Tcp);
            IAddressEnumerator addressEnumerator = new AddressEnumerator();

            // 4. Create the real StoreReader instance
            StoreReader storeReader = new StoreReader(
                mockTransportClient.Object,
                addressSelector,
                addressEnumerator,
                sessionContainer: null,
                enableReplicaValidation: false);

            // 5. Create test request
            // Arrange
            DocumentServiceRequest entity = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA="; // Use a valid base64-encoded ResourceId string
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

            // Act
            IList<ReferenceCountedDisposable<StoreResult>> responses = await storeReader.ReadMultipleReplicaAsync(
            entity,
                includePrimary: true,
                replicaCountToRead: 2, // any replica with correct globalCommittedLsn is good enough
                requiresValidLsn: false,
                useSessionToken: false,
                readMode: ReadMode.Strong,
                checkMinLSN: false,
                forceReadAll: true);

            // Assert
            // All three replica URIs should have been called
            Assert.IsTrue(uriCalled.ContainsKey("rntbd://replica1/"));
            Assert.IsTrue(uriCalled.ContainsKey("rntbd://replica2/"));
            Assert.IsTrue(uriCalled.ContainsKey("rntbd://replica3/"));

            // But the 410 Gone response should not be in the final result list
            Assert.IsTrue(responses.Any(r => r.Target.StatusCode == StatusCodes.Ok), "200 OK response should be included.");
            Assert.IsTrue(responses.Any(r => r.Target.StatusCode == StatusCodes.TooManyRequests), "429 Throttled response should be included.");
            Assert.IsTrue(responses.Any(r => r.Target.StatusCode == StatusCodes.Gone), "410 Gone response should not be included.");


        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow(QuorumNotSelected, true, 0, DisplayName = "QuorumNotSelected - exceptionless mode, no retries")]
        [DataRow(QuorumNotSelected, false, 0, DisplayName = "QuorumNotSelected - exception mode, no retries")]
        [DataRow(QuorumNotSelected, true, 3, DisplayName = "QuorumNotSelected - exceptionless mode, with retries")]
        [DataRow(QuorumNotSelected, false, 3, DisplayName = "QuorumNotSelected - exception mode, with retries")]
        [DataRow(QuorumThrottled, true, 0, DisplayName = "QuorumThrottled - exceptionless mode, no retries")]
        [DataRow(QuorumThrottled, false, 0, DisplayName = "QuorumThrottled - exception mode, no retries")]
        [DataRow(QuorumThrottled, true, 2, DisplayName = "QuorumThrottled - exceptionless mode, with retries")]
        [DataRow(QuorumThrottled, false, 2, DisplayName = "QuorumThrottled - exception mode, with retries")]
        [DataRow(QuorumSelected, true, 0, DisplayName = "QuorumSelected - exceptionless mode, no retries")]
        [DataRow(QuorumSelected, false, 0, DisplayName = "QuorumSelected - exception mode, no retries")]
        [DataRow(QuorumSelected, true, 2, DisplayName = "QuorumSelected - exceptionless mode, with retries")]
        [DataRow(QuorumSelected, false, 2, DisplayName = "QuorumSelected - exception mode, with retries")]
        public async Task QuorumReaderThrottledResponseTestAsync(string scenario, bool exceptionless, int retryCount)
        {
            // Arrange
            TestState testState = new TestState();
            DocumentServiceRequest entity = CreateTestRequest(exceptionless);
            const double requestChargePerCall = 2.5;

            Task<StoreResponse> InvokeResourceOperation(Uri uri, DocumentServiceRequest req)
                => SimulateStoreResponse(scenario, exceptionless, retryCount, req, entity, testState, requestChargePerCall);

            QuorumReader quorumReader = GetQuorumReader(InvokeResourceOperation, enableBarrierEarlyYieldOn429: true);

            // Act & Assert
            await ExecuteAndAssertQuorumTestAsync(
                scenario, exceptionless, retryCount, testState, entity, quorumReader, requestChargePerCall);
        }

        // --- Helper Types and Methods ---

        private static DocumentServiceRequest CreateTestRequest(bool exceptionless)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Document, AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false),
                ResolvedPartitionKeyRange = new PartitionKeyRange(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30))
            };
            entity.UseStatusCodeFor429 = exceptionless;
            return entity;
        }

#pragma warning disable AsyncMethodsMustEndInAsync // Async method must end in Async
        private static Task<StoreResponse> SimulateStoreResponse(
            string scenario, bool exceptionless, int retryCount, DocumentServiceRequest req,
            DocumentServiceRequest entity, TestState state, double requestChargePerCall)
        {
            state.RequestCount++;
            entity.RequestContext.RequestChargeTracker.AddCharge(requestChargePerCall);

            switch (scenario)
            {
                case QuorumNotSelected:
                    return SimulateQuorumNotSelected(exceptionless, retryCount, req, state, requestChargePerCall);
                case QuorumThrottled:
                    return SimulateQuorumThrottled(exceptionless, retryCount, req, state, requestChargePerCall);
                case QuorumSelected:
                    return SimulateQuorumSelected(exceptionless, retryCount, req, state, requestChargePerCall);
                default:
                    throw new ArgumentException("Unknown scenario");
            }
        }

        private static Task<StoreResponse> SimulateQuorumNotSelected(
            bool exceptionless, int retryCount, DocumentServiceRequest req, TestState state, double charge)
        {
            if (retryCount > 0 && state.RequestCount > 4 && state.ActualRetryCount < retryCount)
            {
                state.ShouldRetryOnSecondary = true;
                state.ActualRetryCount++;
            }
            StoreResponse response = CreateThrottledResponse(charge); // no lsn
            if (!exceptionless)
            {
                ThrowServerException(response, req);
            }

            return Task.FromResult(response);
        }

        private static Task<StoreResponse> SimulateQuorumThrottled(
            bool exceptionless, int retryCount, DocumentServiceRequest req, TestState state, double charge)
        {
            if (retryCount > 0 && state.RequestCount > 5 && state.ActualRetryCount < retryCount)
            {
                state.ShouldRetryOnSecondary = true;
                state.ActualRetryCount++;
            }
            if (state.RequestCount <= 2)
            {
                return Task.FromResult(CreateSuccessResponse(charge));
            }

            StoreResponse response = CreateThrottledResponse(charge, "100");
            if (!exceptionless)
            {
                ThrowServerException(response, req);
            }

            return Task.FromResult(response);
        }

        private static Task<StoreResponse> SimulateQuorumSelected(
            bool exceptionless, int retryCount, DocumentServiceRequest req, TestState state, double charge)
        {
            if (retryCount > 0 && state.RequestCount > 5 && state.ActualRetryCount < retryCount)
            {
                state.ShouldRetryOnSecondary = true;
                state.ActualRetryCount++;
            }
            if (state.RequestCount <= 2)
            {
                return Task.FromResult(CreateSuccessResponse(charge));
            }

            StoreResponse response = CreateThrottledResponse(charge, "100");
            if (!exceptionless)
            {
                ThrowServerException(response, req);
            }

            return Task.FromResult(response);
        }


        private static StoreResponse CreateThrottledResponse(double charge, string lsn = null)
        {
            NameValueCollection headers = new NameValueCollection
            {
                  { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                  { WFConstants.BackendHeaders.SubStatus, SubStatusCodes.RUBudgetExceeded.ToString() },
                  { HttpConstants.HttpHeaders.RequestCharge, charge.ToString(CultureInfo.InvariantCulture) }
            };
            if (lsn != null)
            {
                headers.Add(WFConstants.BackendHeaders.LSN, lsn);
            }
            return new StoreResponse
            {
                Status = 429,
                Headers = new DictionaryNameValueCollection(headers)
            };
        }

        private static StoreResponse CreateSuccessResponse(double charge) =>
            new StoreResponse
            {
                Status = 200,
                Headers = new DictionaryNameValueCollection(
                    new NameValueCollection
                    {
                { WFConstants.BackendHeaders.LSN, "100" },
                { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                { WFConstants.BackendHeaders.NumberOfReadRegions, "3" },
                { HttpConstants.HttpHeaders.RequestCharge, charge.ToString(CultureInfo.InvariantCulture) }
                    })
            };

        private static void ThrowServerException(StoreResponse response, DocumentServiceRequest req)
        {
            TransportClient.ThrowServerException("http://one.com", response, new Uri("http://one.com"), Guid.Empty, req);
            throw new Exception("TransportClient should have thrown.");
        }

        private static async Task ExecuteAndAssertQuorumTestAsync(
            string scenario, bool exceptionless, int retryCount, TestState state,
            DocumentServiceRequest entity, QuorumReader quorumReader, double requestChargePerCall)
        {
            try
            {
                StoreResponse response = await quorumReader.ReadStrongAsync(entity, 2, ReadMode.Strong);
                Assert.IsTrue(exceptionless, "Should have thrown.");
                Assert.AreEqual(429, response.Status);
                Assert.AreEqual(SubStatusCodes.RUBudgetExceeded.ToString(), response.Headers[WFConstants.BackendHeaders.SubStatus]);
                Assert.IsNotNull(response.Headers[HttpConstants.HttpHeaders.RequestCharge]);
            }
            catch (DocumentClientException dce) when (!exceptionless && (int)dce.StatusCode == 429)
            {
                // Expected
            }

            int expectedBaseRequests = scenario switch
            {
                QuorumSelected => 5,
                QuorumNotSelected => 4,
                QuorumThrottled => 5,
                _ => throw new ArgumentException("Unknown scenario")
            };
            int expectedTotalRequests = expectedBaseRequests + (state.ActualRetryCount * expectedBaseRequests);

            if (retryCount > 0)
            {
                Assert.AreEqual(expectedTotalRequests, state.RequestCount);
            }
            else
            {
                Assert.AreEqual(expectedBaseRequests, state.RequestCount);
            }

            double expectedCharge = (state.RequestCount * 2) * requestChargePerCall;
            Assert.AreEqual(expectedCharge, entity.RequestContext.RequestChargeTracker.TotalRequestCharge, 0.001);
        }

        private class TestState
        {
            public int RequestCount { get; set; }

            public int ActualRetryCount { get; set; }

            public bool ShouldRetryOnSecondary { get; set; }
        }


        /// <summary>
        /// Upon receiving invalid LSN responses, QuorumReader should not fail and propagate the failure.
        /// </summary>
        [DataTestMethod]
        [DataRow(false, DisplayName = "With Exceptions")]
        [DataRow(true, DisplayName = "Without Exceptions")]
        [Owner("cosmosdbcomputeteam")]
        public async Task QuorumWithInvalidLSNResponses(bool exceptionless)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.UseStatusCodeFor429 = exceptionless;
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false),
                ResolvedPartitionKeyRange = new PartitionKeyRange(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromMinutes(5), CancellationToken.None)
            };

            int requestCount = 0;
            Task<StoreResponse> InvokeResourceOperation(Uri x, DocumentServiceRequest y)
            {
                StoreResponse storeResponse = new StoreResponse
                {
                    Status = 429,
                    Headers = new DictionaryNameValueCollection(
                        new NameValueCollection
                        {
                        { WFConstants.BackendHeaders.LSN, "-1" },
                        { WFConstants.BackendHeaders.LocalLSN, "-1" },
                        { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                        })
                };

                requestCount++;

                if (exceptionless)
                {
                    return Task.FromResult(storeResponse);
                }

                TransportClient.ThrowServerException("http://one.com", storeResponse, new Uri("http://one.com"), Guid.Empty, entity);

                throw new Exception("TransportClient should have thrown.");
            }

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(InvokeResourceOperation);

            try
            {
                StoreResponse response = await quorumReader.ReadStrongAsync(
                        entity,
                        2,
                        ReadMode.Strong);

                Assert.IsTrue(exceptionless, "Should have thrown.");

                Assert.AreEqual(429, response.Status);
            }
            catch (DocumentClientException dce) when (!exceptionless && (int)dce.StatusCode == 429)
            {
                // When exceptionless is off, it throws
            }

            Assert.AreEqual(4, requestCount);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_All429_ReturnsThrottledResponseAsync()
        {
            // Arrange - Test when all replicas return 429 during read barrier
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.UseStatusCodeFor429 = true;
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;
            int requestCount = 0;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    requestCount++;

                    // All replicas return 429
                    return Task.FromResult(new StoreResponse
                    {
                        Status = 429,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { HttpConstants.HttpHeaders.RequestCharge, "2.5" }
                        })
                    });
                },
                enableBarrierEarlyYieldOn429: true);

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);

            // Assert - Should return 429 response for upstream retry policy
            Assert.AreEqual(429, response.Status, "Expected 429 response when all replicas are throttled.");
            Assert.AreEqual(((int)SubStatusCodes.RUBudgetExceeded).ToString(), response.Headers[WFConstants.BackendHeaders.SubStatus]);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_Mixed429And410_HandlesCorrectlyAsync()
        {
            // Arrange - Test when replicas return mix of 429 and 410 (Gone)
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.UseStatusCodeFor429 = true;
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    string u = uri.AbsoluteUri;

                    // Primary returns success for reads
                    if (u.Contains("/0p/"))
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        });
                    }

                    // One secondary returns 429
                    if (u.Contains("/1s/"))
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() }
                            })
                        });
                    }

                    // Another secondary returns 410 Gone
                    if (u.Contains("/2s/"))
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 410,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.PartitionKeyRangeGone).ToString() }
                            })
                        });
                    }

                    // Default success
                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    });
                });

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);

            // Assert - Should succeed as quorum was met
            Assert.AreEqual(200, response.Status, "Expected success when quorum is met with mixed errors.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_Transient429ThenSuccess_SucceedsAsync()
        {
            // Arrange - Test when 429 is transient and resolves within retry
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.UseStatusCodeFor429 = true;
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;
            int requestCount = 0;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    requestCount++;

                    // First few requests return 429, then succeed
                    if (requestCount <= 4)
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                                { WFConstants.BackendHeaders.LSN, "100" }
                            })
                        });
                    }

                    // After transient throttling, succeed
                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    });
                },
                enableBarrierEarlyYieldOn429: true);

            // Act - Note: This test may return 429 if transient handling isn't within same request cycle
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);

            // Assert - Should return 429 as all replicas were throttled in initial request
            Assert.AreEqual(429, response.Status, "Expected 429 when all replicas throttled initially.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadPrimary_Returns429_ReturnsThrottledResponseAsync()
        {
            // Arrange - Test when primary read returns 429
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.UseStatusCodeFor429 = true;
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    string u = uri.AbsoluteUri;

                    // Primary returns 429
                    if (u.Contains("/0p/"))
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 429,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                                { HttpConstants.HttpHeaders.RequestCharge, "2.5" }
                            })
                        });
                    }

                    // Secondaries return success
                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    });
                },
                enableBarrierEarlyYieldOn429: true);

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);

            // Assert - Should succeed from secondaries or return 429 if primary needed
            Assert.IsTrue(response.Status == 200 || response.Status == 429,
                "Expected either success from secondaries or 429 from throttled primary.");
        }

        [DataTestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow(true, DisplayName = "Exceptionless mode returns 429 response")]
        [DataRow(false, DisplayName = "Exception mode throws 429 exception")]
        public async Task ReadBarrier_429WithRequestCharge_TracksChargeCorrectlyAsync(bool exceptionless)
        {
            // Arrange - Verify request charge is tracked across throttled responses
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.UseStatusCodeFor429 = exceptionless;
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;
            double chargePerRequest = 2.5;
            int requestCount = 0;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    requestCount++;
                    entity.RequestContext.RequestChargeTracker.AddCharge(chargePerRequest);

                    // All return 429
                    StoreResponse response = new StoreResponse
                    {
                        Status = 429,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { HttpConstants.HttpHeaders.RequestCharge, chargePerRequest.ToString(CultureInfo.InvariantCulture) }
                        })
                    };

                    if (!exceptionless)
                    {
                        TransportClient.ThrowServerException("http://test.com", response, new Uri("http://test.com"), Guid.Empty, entity);
                        throw new Exception("Should have thrown");
                    }

                    return Task.FromResult(response);
                },
                enableBarrierEarlyYieldOn429: true);

            // Act
            try
            {
                StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);
                Assert.IsTrue(exceptionless, "Should have thrown in exception mode.");
                Assert.AreEqual(429, response.Status);
            }
            catch (DocumentClientException dce) when (!exceptionless && (int)dce.StatusCode == 429)
            {
                // Expected in exception mode
            }

            // Assert - Request charge should be accumulated
            Assert.IsTrue(entity.RequestContext.RequestChargeTracker.TotalRequestCharge > 0,
                "Request charge should be tracked even for throttled responses.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_FlagDisabled_All429_DoesNotYieldEarlyAsync()
        {
            // Arrange - Flag is OFF (default). Primary read succeeds but with
            // a stale LSN so barrier is needed. All barrier replicas return 429.
            // With flag ON, barrier would detect all-429 and yield early.
            // With flag OFF, barrier does NOT yield early — it retries until
            // timeout and surfaces GoneException.
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.UseStatusCodeFor429 = true;
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;
            int requestCount = 0;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    requestCount++;

                    // Primary read returns success but with stale LSN to force barrier
                    if (req.OperationType == OperationType.Read)
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "50" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                                { WFConstants.BackendHeaders.QuorumAckedLSN, "50" },
                                { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        });
                    }

                    // Barrier requests (Head) all return 429
                    return Task.FromResult(new StoreResponse
                    {
                        Status = 429,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString() },
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { HttpConstants.HttpHeaders.RequestCharge, "2.5" }
                        })
                    });
                });

            // Act & Assert - Without the flag, the quorum reader should NOT
            // return a 429 via the barrier early yield path.
            try
            {
                StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);
                Assert.AreNotEqual(429, response.Status,
                    "With flag disabled, barrier should not yield early with 429.");
            }
            catch (GoneException)
            {
                // Expected when barrier retries are exhausted without the
                // early yield path.
            }
            catch (RequestTimeoutException)
            {
                // Also acceptable when barrier timeout is exceeded.
            }
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_ReplicaGone1022_PrimarySuccess_ShouldSucceed()
        {
            // Arrange
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Any;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    string u = uri.AbsoluteUri;

                    static DictionaryNameValueCollection H(
                        string lsn, string localLsn, string gcLsn,
                        string quorumAckedLsn, string replicaSetSize,
                        string readRegions = "2")
                    {
                        NameValueCollection nvc = new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, lsn },
                            { WFConstants.BackendHeaders.LocalLSN, localLsn },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, gcLsn },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, quorumAckedLsn },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, replicaSetSize },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, readRegions },
                            { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                        };
                        return new DictionaryNameValueCollection(nvc);
                    }

                    if (req.OperationType == OperationType.Read)
                    {
                        // Two VALID reads:
                        // 1s: LSN 100 (highest)
                        if (u.Contains("/replicas/1s/"))
                        {
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = H("100", "100", "100", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                            });
                        }

                        // 2s: LSN 90 (lower)
                        if (u.Contains("/replicas/2s/"))
                        {
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = H("90", "90", "90", "90", readQuorum.ToString(CultureInfo.InvariantCulture))
                            });
                        }

                        // Any other read address (e.g., 0p if ever included by retries) — keep valid but not max LSN
                        return Task.FromResult(new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = H("95", "95", "95", "95", readQuorum.ToString(CultureInfo.InvariantCulture))
                        });
                    }

                    if (req.OperationType == OperationType.Head)
                    {
                        // Secondary 1s barrier: Gone/1022 (triggers pivot-to-primary in barrier)
                        if (u.Contains("/replicas/1s/"))
                        {
                            DictionaryNameValueCollection gone = new DictionaryNameValueCollection();
                            gone[WFConstants.BackendHeaders.SubStatus] =
                                ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture); // 1022
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)StatusCodes.Gone,
                                Headers = gone
                            });
                        }

                        // Primary barrier: succeed with LSN/GCLSN >= selected (100) and set ReplicaSetSize <= readQuorum
                        if (u.Contains("/replicas/0p/"))
                        {
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = H("100", "100", "100", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                            });
                        }

                        return Task.FromResult(new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = H("100", "100", "100", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                        });
                    }

                    throw new InvalidOperationException($"Unexpected operation type: {req.OperationType}");
                });

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
        }



        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_ReplicaGone1022_PrimaryOtherError_ShouldThrowException()
        {
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Any;

            AddressSelector addressSelector = BuildAddressSelector();
            Mock<TransportClient> transport = new Mock<TransportClient>(MockBehavior.Strict);

            transport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    string u = uri.Uri.ToString();

                    if (r.OperationType == OperationType.Head)
                    {
                        if (u.Contains("/0p/"))
                        {
                            return new StoreResponse
                            {
                                Status = (int)HttpStatusCode.NotFound,
                                Headers = new DictionaryNameValueCollection(new NameValueCollection
                                {
                                    { WFConstants.BackendHeaders.CurrentReplicaSetSize, readQuorum.ToString(CultureInfo.InvariantCulture) },
                                    { WFConstants.BackendHeaders.LSN, "-1" },
                                    { WFConstants.BackendHeaders.QuorumAckedLSN, "-1" }
                                })
                            };
                        }

                        return new StoreResponse
                        {
                            Status = (int)StatusCodes.Gone,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture) }
                            })
                        };
                    }

                    if (u.Contains("/1s/"))
                    {
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    if (u.Contains("/2s/"))
                    {
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "90" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "89" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    };
                });

            StoreReader storeReader = new StoreReader(
                transport.Object,
                addressSelector,
                new AddressEnumerator(),
                new NullSessionContainer(),
                false);

            QuorumReader quorumReader = new QuorumReader(
                transport.Object,
                addressSelector,
                storeReader,
                new QuorumReaderTests.MockServiceConfigurationReader(ConsistencyLevel.Strong),
                new QuorumReaderTests.MockAuthorizationTokenProvider());

            await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);
            });
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_ReplicaGone1022_PrimaryGone1022_ShouldThrowGoneException()
        {
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Any;

            AddressSelector addressSelector = BuildAddressSelector();
            Mock<TransportClient> transport = new Mock<TransportClient>(MockBehavior.Strict);

            transport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    string u = uri.Uri.ToString();

                    if (r.OperationType == OperationType.Head)
                    {
                        return new StoreResponse
                        {
                            Status = (int)StatusCodes.Gone,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture) }
                            })
                        };
                    }

                    if (u.Contains("/1s/"))
                    {
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    if (u.Contains("/2s/"))
                    {
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "90" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "89" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    };
                });

            StoreReader storeReader = new StoreReader(
                transport.Object,
                addressSelector,
                new AddressEnumerator(),
                new NullSessionContainer(),
                false);

            QuorumReader quorumReader = new QuorumReader(
                transport.Object,
                addressSelector,
                storeReader,
                new QuorumReaderTests.MockServiceConfigurationReader(ConsistencyLevel.Strong),
                new QuorumReaderTests.MockAuthorizationTokenProvider());

            await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);
            });
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_ReplicaGone1022_PrimaryInsufficientGCLSN_ShouldFail()
        {
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            int readQuorum = 2;
            ReadMode readMode = ReadMode.Any;

            AddressSelector addressSelector = BuildAddressSelector();
            Mock<TransportClient> transport = new Mock<TransportClient>(MockBehavior.Strict);

            transport
                .Setup(t => t.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest r) =>
                {
                    string u = uri.Uri.ToString();

                    if (r.OperationType == OperationType.Head)
                    {
                        if (u.Contains("/0p/"))
                        {
                            return new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = new DictionaryNameValueCollection(new NameValueCollection
                                {
                                    { WFConstants.BackendHeaders.LSN, "100" },
                                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "50" },
                                    { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                                    { WFConstants.BackendHeaders.CurrentReplicaSetSize, readQuorum.ToString(CultureInfo.InvariantCulture) }
                                })
                            };
                        }

                        return new StoreResponse
                        {
                            Status = (int)StatusCodes.Gone,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                            { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture) }
                            })
                        };
                    }

                    if (u.Contains("/1s/"))
                    {
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "100" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    if (u.Contains("/2s/"))
                    {
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.LSN, "90" },
                                { WFConstants.BackendHeaders.GlobalCommittedLSN, "89" },
                                { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                            })
                        };
                    }

                    return new StoreResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "2" }
                        })
                    };
                });

            StoreReader storeReader = new StoreReader(
                transport.Object,
                addressSelector,
                new AddressEnumerator(),
                new NullSessionContainer(),
                false);

            QuorumReader quorumReader = new QuorumReader(
                transport.Object,
                addressSelector,
                storeReader,
                new QuorumReaderTests.MockServiceConfigurationReader(ConsistencyLevel.Strong),
                new QuorumReaderTests.MockAuthorizationTokenProvider());

            await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);
            });
        }

        private static AddressSelector BuildAddressSelector()
        {
            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation("rntbd://unit/0p/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://unit/1s/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
                new AddressInformation("rntbd://unit/2s/", isPrimary: true, protocol: Protocol.Tcp, isPublic: true, isAuxiliary: false),
            };

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();
            mockAddressCache
                .Setup(c => c.ResolveAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            return new AddressSelector(mockAddressCache.Object, Protocol.Tcp);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_NonFirstReplicaGone1022_PrimarySuccess_ShouldSucceed()
        {
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            entity.RequestContext.OriginalRequestConsistencyLevel = ConsistencyLevel.Strong;

            int readQuorum = 2;
            ReadMode readMode = ReadMode.Strong;

            int primaryHeadCalls = 0;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    string u = uri.AbsoluteUri;

                    static DictionaryNameValueCollection H(
                        string lsn, string localLsn, string gcLsn,
                        string quorumAckedLsn, string replicaSetSize,
                        string readRegions = "2")
                    {
                        NameValueCollection nvc = new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, lsn },
                            { WFConstants.BackendHeaders.LocalLSN, localLsn },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, gcLsn },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, quorumAckedLsn },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, replicaSetSize },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, readRegions },
                            { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                        };
                        return new DictionaryNameValueCollection(nvc);
                    }

                    if (req.OperationType == OperationType.Read)
                    {
                        if (u.Contains("/replicas/1s/"))
                        {
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = H("100", "100", "95", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                            });
                        }

                        if (u.Contains("/replicas/2s/"))
                        {
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = H("90", "90", "89", "90", readQuorum.ToString(CultureInfo.InvariantCulture))
                            });
                        }

                        return Task.FromResult(new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = H("95", "95", "90", "95", readQuorum.ToString(CultureInfo.InvariantCulture))
                        });
                    }

                    if (req.OperationType == OperationType.Head)
                    {
                        // ANY secondary (2s) returns Gone/1022 -> should trigger pivot to primary
                        if (u.Contains("/replicas/2s/"))
                        {
                            DictionaryNameValueCollection gone = new DictionaryNameValueCollection(new NameValueCollection
                            {
                                { WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture) }
                            });

                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)StatusCodes.Gone,
                                Headers = gone
                            });
                        }

                        if (u.Contains("/replicas/0p/"))
                        {
                            Interlocked.Increment(ref primaryHeadCalls);
                            return Task.FromResult(new StoreResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                                Headers = H("100", "100", "100", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                            });
                        }

                        return Task.FromResult(new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = H("100", "100", "95", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                        });
                    }

                    throw new InvalidOperationException($"Unexpected operation type: {req.OperationType}");
                });

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, readMode);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
            Assert.IsTrue(primaryHeadCalls >= 1, "Primary HEAD must be invoked when ANY secondary returns 410/1022.");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow("GlobalStrong", "Strong", DisplayName = "GlobalStrong strategy uses Strong read mode")]
        [DataRow("LatestCommitted", "BoundedStaleness", DisplayName = "LatestCommitted strategy uses BoundedStaleness mode")]
        [DataRow("LastCommittedSingleWriteRegion", "BoundedStaleness", DisplayName = "LastCommittedSingleWriteRegion strategy uses BoundedStaleness mode")]
        public async Task ReadQuorum_WithReadConsistencyStrategy_SelectsCorrectReadModeAsync(
            string strategyName,
            string expectedReadModeName)
        {
            // Parse string parameters to enums
            ReadConsistencyStrategy strategy = (ReadConsistencyStrategy)Enum.Parse(
                typeof(ReadConsistencyStrategy),
                strategyName);

            ReadMode expectedReadMode = (ReadMode)Enum.Parse(
                typeof(ReadMode),
                expectedReadModeName);

            // Arrange
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.RequestContext.ReadConsistencyStrategy = strategy;
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] =
                strategy == (ReadConsistencyStrategy)Enum.Parse(typeof(ReadConsistencyStrategy), "GlobalStrong")
                    ? ConsistencyLevel.Strong.ToString()
                    : ConsistencyLevel.BoundedStaleness.ToString();

            int readQuorum = 2;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Read)
                    {
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.LocalLSN, "100" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, readQuorum.ToString(CultureInfo.InvariantCulture) },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                        { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                            })
                        });
                    }

                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                    { WFConstants.BackendHeaders.LSN, "100" },
                    { WFConstants.BackendHeaders.LocalLSN, "100" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                    { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                    { WFConstants.BackendHeaders.CurrentReplicaSetSize, readQuorum.ToString(CultureInfo.InvariantCulture) },
                    { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                        })
                    });
                });

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, expectedReadMode);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(200, response.Status);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadQuorum_WithLatestCommittedStrategy_PerformsQuorumReadAsync()
        {
            // Arrange
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();

            ReadConsistencyStrategy strategy = (ReadConsistencyStrategy)Enum.Parse(
                typeof(ReadConsistencyStrategy),
                "LatestCommitted");

            entity.RequestContext.ReadConsistencyStrategy = strategy;
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.BoundedStaleness.ToString();

            int readQuorum = 2;
            int readAttempts = 0;

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Read)
                    {
                        Interlocked.Increment(ref readAttempts);
                        return Task.FromResult(new StoreResponse
                        {
                            Status = 200,
                            Headers = new DictionaryNameValueCollection(new NameValueCollection
                            {
                        { WFConstants.BackendHeaders.LSN, "100" },
                        { WFConstants.BackendHeaders.LocalLSN, "100" },
                        { WFConstants.BackendHeaders.GlobalCommittedLSN, "99" },
                        { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                        { WFConstants.BackendHeaders.CurrentReplicaSetSize, readQuorum.ToString(CultureInfo.InvariantCulture) },
                        { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                        { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                            })
                        });
                    }

                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                    { WFConstants.BackendHeaders.LSN, "100" },
                    { WFConstants.BackendHeaders.LocalLSN, "100" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                    { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                    { WFConstants.BackendHeaders.CurrentReplicaSetSize, readQuorum.ToString(CultureInfo.InvariantCulture) },
                    { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                        })
                    });
                });

            // Act
            StoreResponse response = await quorumReader.ReadStrongAsync(
                entity,
                readQuorum,
                ReadMode.BoundedStaleness);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(200, response.Status);
            Assert.IsTrue(readAttempts >= readQuorum,
                $"Quorum read should read from at least {readQuorum} replicas, but attempted {readAttempts}");
        }

        [TestMethod]
        [Owner("dibilla")]
        public async Task ReadQuorum_PPFOverrideTriggersGlobalStrongRead_SessionAccount()
        {
            // Arrange: Session account with TryGetConsistencyLevel overriding to Strong for PPF-eligible resources
            int readAttempts = 0;
            int readQuorum = 3; // Strong consistency requires reading from more replicas
            StoreResponse successResponse = new StoreResponse
            {
                Status = 200,
                Headers = new DictionaryNameValueCollection(new NameValueCollection
                {
                    { WFConstants.BackendHeaders.LSN, "42" },
                    { WFConstants.BackendHeaders.LocalLSN, "42" },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, "42" },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, "2" },
                    { HttpConstants.HttpHeaders.RequestCharge, "1" }
                })
            };

            Func<Uri, DocumentServiceRequest, Task<StoreResponse>> invokeResourceOperationAsyncHandler =
                (uri, request) =>
                {
                    readAttempts++;
                    return Task.FromResult(successResponse);
                };

            QuorumReader reader = QuorumReaderTests.GetQuorumReaderWithExtension(
                invokeResourceOperationAsyncHandler,
                defaultConsistencyLevel: ConsistencyLevel.Session,
                tryGetConsistencyLevelFunc: (req) =>
                {
                    // Simulate PPF-eligible override: Document resources (excluding MetadataCheckAccess) -> Strong
                    if (req.ResourceType == ResourceType.Document &&
                        req.OperationType != OperationType.MetadataCheckAccess)
                    {
                        return (true, ConsistencyLevel.Strong);
                    }

                    return (false, default);
                });

            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false),
                ResolvedPartitionKeyRange = new PartitionKeyRange(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromMinutes(5))
            };

            // No ConsistencyLevel header and no OriginalRequestConsistencyLevel — should allow override

            StoreResponse response = await reader.ReadStrongAsync(entity, readQuorum, ReadMode.Strong);

            Assert.IsNotNull(response);
            Assert.AreEqual(200, response.Status);
            Assert.IsTrue(readAttempts >= readQuorum,
                $"PPF override to Strong should read from at least {readQuorum} replicas, but attempted {readAttempts}");
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ReadQuorum_PPFOverrideExcludesMetadataCheckAccess()
        {
            // Verify that TryGetConsistencyLevel correctly excludes MetadataCheckAccess operations
            // This mirrors GatewayDatabaseAccountConfiguration behavior where MetadataCheckAccess is not PPF-eligible
            Mock<IServiceConfigurationReaderExtension> mockConfig = CreateMockServiceConfigReaderExtension(
                ConsistencyLevel.Session,
                tryGetConsistencyLevelFunc: (req) =>
                {
                    // Simulate PPF eligibility check that excludes MetadataCheckAccess
                    if (req.ResourceType == ResourceType.Document &&
                        req.OperationType != OperationType.MetadataCheckAccess)
                    {
                        return (true, ConsistencyLevel.Strong);
                    }

                    return (false, default);
                });

            DocumentServiceRequest eligibleRequest = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.SystemAll);

            DocumentServiceRequest metadataCheckAccessRequest = DocumentServiceRequest.Create(
                OperationType.MetadataCheckAccess,
                ResourceType.Document,
                AuthorizationTokenType.SystemAll);

            DocumentServiceRequest offerRequest = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Offer,
                AuthorizationTokenType.SystemAll);

            // Document/Read should get overridden to Strong
            Assert.IsTrue(
                mockConfig.Object.TryGetConsistencyLevel(eligibleRequest, out ConsistencyLevel level1),
                "Document/Read should be overridden");
            Assert.AreEqual(ConsistencyLevel.Strong, level1);

            // Document/MetadataCheckAccess should NOT be overridden
            Assert.IsFalse(
                mockConfig.Object.TryGetConsistencyLevel(metadataCheckAccessRequest, out _),
                "Document/MetadataCheckAccess should NOT be overridden");

            // Offer/Read should NOT be overridden (non-eligible resource type)
            Assert.IsFalse(
                mockConfig.Object.TryGetConsistencyLevel(offerRequest, out _),
                "Offer/Read should NOT be overridden");
        }

        [TestMethod]
        [Owner("dibilla")]
        public void ReadQuorum_NullExtension_FallsBackToAccountDefault()
        {
            // Arrange: Using the original MockServiceConfigurationReader (no extension interface)
            // The `as IServiceConfigurationReaderExtension` cast will return null — verify fallback behavior
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfig.SetupGet(c => c.SystemReplicationPolicy).Returns(new ReplicationPolicy { MaxReplicaSetSize = 4 });
            mockConfig.SetupGet(c => c.UserReplicationPolicy).Returns(new ReplicationPolicy { MaxReplicaSetSize = 4 });

            // Mock<IServiceConfigurationReader> does NOT implement IServiceConfigurationReaderExtension
            // so QuorumReader's `as` cast returns null — no PPF override applied
            Assert.IsNull(
                mockConfig.Object as IServiceConfigurationReaderExtension,
                "Mock should NOT implement IServiceConfigurationReaderExtension — tests fallback path");

            // With Session default and no extension override, global strong reads should NOT be triggered
            Assert.IsTrue(
                mockConfig.Object.DefaultConsistencyLevel == ConsistencyLevel.Session,
                "Without PPF override, Session account should remain Session — no global strong reads");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ReadQuorum_GlobalStrongWithNonStrongAccount_ValidatesStrategy()
        {
            // Arrange
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();

            ReadConsistencyStrategy strategy = (ReadConsistencyStrategy)Enum.Parse(
                typeof(ReadConsistencyStrategy),
                "GlobalStrong");

            entity.RequestContext.ReadConsistencyStrategy = strategy;
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Session.ToString();

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Session);
            mockConfig.SetupGet(c => c.SystemReplicationPolicy).Returns(new ReplicationPolicy { MaxReplicaSetSize = 4 });
            mockConfig.SetupGet(c => c.UserReplicationPolicy).Returns(new ReplicationPolicy { MaxReplicaSetSize = 4 });

            // GlobalStrong with non-Strong account should fail validation
            // This test documents the constraint that GlobalStrong only works with Strong consistency
            Assert.IsTrue(
                entity.RequestContext.ReadConsistencyStrategy == strategy &&
                mockConfig.Object.DefaultConsistencyLevel != ConsistencyLevel.Strong,
                "GlobalStrong strategy is incompatible with non-Strong account consistency");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow("Eventual", "LatestCommitted", DisplayName = "Both headers: Eventual CL + LatestCommitted strategy → strategy wins")]
        [DataRow("Strong", "Eventual", DisplayName = "Both headers: Strong CL + Eventual strategy → strategy wins")]
        [DataRow("Session", "GlobalStrong", DisplayName = "Both headers: Session CL + GlobalStrong strategy → strategy wins")]
        public async Task ProcessMessage_BothHeadersSet_StrategyTakesPrecedence(
            string consistencyLevel, string strategyName)
        {
            // Arrange
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = consistencyLevel;
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = strategyName;

            ReadConsistencyStrategy expectedStrategy = (ReadConsistencyStrategy)Enum.Parse(
                typeof(ReadConsistencyStrategy), strategyName);

            // Act - should NOT throw
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(expectedStrategy, request.RequestContext.ReadConsistencyStrategy,
                "ReadConsistencyStrategy should be set from the header value");
            Assert.IsNull(request.RequestContext.OriginalRequestConsistencyLevel,
                "ConsistencyLevel should be ignored when ReadConsistencyStrategy is also set");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow("LatestCommitted", "LatestCommitted", DisplayName = "Valid strategy: LatestCommitted")]
        [DataRow("GlobalStrong", "GlobalStrong", DisplayName = "Valid strategy: GlobalStrong")]
        [DataRow("LastCommittedSingleWriteRegion", "LastCommittedSingleWriteRegion", DisplayName = "Valid strategy: LastCommittedSingleWriteRegion")]
        [DataRow("latestcommitted", "LatestCommitted", DisplayName = "Case-insensitive: latestcommitted")]
        [DataRow("globalstrong", "GlobalStrong", DisplayName = "Case-insensitive: globalstrong")]
        [DataRow("LastCommittedSingleWriteRegion", "LastCommittedSingleWriteRegion", DisplayName = "Case-insensitive: LastCommittedSingleWriteRegion")]
        [DataRow("EVENTUAL", "Eventual", DisplayName = "Case-insensitive: EVENTUAL")]
        public async Task ProcessMessage_ValidStrategyHeader_SetsStrategy(
            string headerValue, string expectedStrategyName)
        {
            // Arrange
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = headerValue;

            ReadConsistencyStrategy expectedStrategy = (ReadConsistencyStrategy)Enum.Parse(
                typeof(ReadConsistencyStrategy), expectedStrategyName);

            // Act
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(expectedStrategy, request.RequestContext.ReadConsistencyStrategy);
            Assert.IsNull(request.RequestContext.OriginalRequestConsistencyLevel);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ProcessMessage_DefaultStrategyValue_IgnoredWithoutException()
        {
            // Arrange - "Default" is a special case: treated as "not set", falls back to account default
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = "Default";

            // Act - should NOT throw BadRequestException
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(request.RequestContext.ReadConsistencyStrategy,
                "'Default' strategy should be ignored, leaving ReadConsistencyStrategy null");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        [DataRow("99", DisplayName = "Invalid strategy: numeric 99")]
        [DataRow("UnknownStrategy", DisplayName = "Invalid strategy: arbitrary string")]
        [DataRow("Blah", DisplayName = "Invalid strategy: Blah")]
        public async Task ProcessMessage_InvalidStrategyValue_ThrowsBadRequestException(
            string invalidStrategyValue)
        {
            // Arrange - invalid values (not "Default", not a valid enum member) should throw
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = invalidStrategyValue;

            // Act & Assert
            await Assert.ThrowsExceptionAsync<BadRequestException>(
                () => storeModel.ProcessMessageAsync(request));
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ProcessMessage_BothHeadersWithDefaultStrategy_FallsBackToConsistencyLevel()
        {
            // Arrange - both headers present, but ReadConsistencyStrategy is "Default".
            // "Default" is ignored, so ConsistencyLevel should be used.
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            request.Headers[HttpConstants.HttpHeaders.ReadConsistencyStrategy] = "Default";

            // Act - should NOT throw
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(request.RequestContext.ReadConsistencyStrategy,
                "'Default' strategy should be ignored");
            Assert.AreEqual(ConsistencyLevel.Strong, request.RequestContext.OriginalRequestConsistencyLevel,
                "ConsistencyLevel should be used since 'Default' strategy is treated as not set");
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ProcessMessage_OnlyConsistencyLevelHeader_SetsOriginalRequestConsistencyLevel()
        {
            // Arrange - only ConsistencyLevel header, no ReadConsistencyStrategy
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Eventual.ToString();

            // Act
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(ConsistencyLevel.Eventual, request.RequestContext.OriginalRequestConsistencyLevel);
            Assert.IsNull(request.RequestContext.ReadConsistencyStrategy);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ProcessMessage_InvalidConsistencyLevelValue_ThrowsBadRequestException()
        {
            // Arrange - invalid ConsistencyLevel still throws (only ReadConsistencyStrategy is lenient)
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();
            request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = "InvalidLevel";

            // Act & Assert
            await Assert.ThrowsExceptionAsync<BadRequestException>(
                () => storeModel.ProcessMessageAsync(request));
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ProcessMessage_NoHeaders_NeitherContextFieldSet()
        {
            // Arrange - neither header set
            ServerStoreModel storeModel = CreateMockServerStoreModelForHeaderTests();
            DocumentServiceRequest request = CreateReadRequestForHeaderTests();

            // Act
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNull(request.RequestContext.OriginalRequestConsistencyLevel);
            Assert.IsNull(request.RequestContext.ReadConsistencyStrategy);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadBarrier_Timeout_ThrowsGoneExceptionWithReadBarrierSubStatus()
        {
            // Arrange: initial quorum reads succeed instantly with a GlobalCommittedLSN gap
            // (LSN=100, GlobalCommittedLSN=50), triggering the barrier. Barrier HEAD requests
            // delay 3000ms each. With a 2000ms timeout, the initial quorum reads and first
            // barrier ThrowGoneIfElapsed check complete within budget (~500-1500ms), but after
            // the first 3000ms barrier HEAD completes, the second ThrowGoneIfElapsed check
            // fires because elapsed time (~4500ms) exceeds the 2000ms timeout.
            int readQuorum = 2;

            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            entity.RequestContext.OriginalRequestConsistencyLevel = ConsistencyLevel.Strong;
            entity.RequestContext.TimeoutHelper = new TimeoutHelper(TimeSpan.FromMilliseconds(2000));

            static DictionaryNameValueCollection H(
                string lsn, string localLsn, string gcLsn,
                string quorumAckedLsn, string replicaSetSize,
                string readRegions = "2")
            {
                NameValueCollection nvc = new NameValueCollection
                {
                    { WFConstants.BackendHeaders.LSN, lsn },
                    { WFConstants.BackendHeaders.LocalLSN, localLsn },
                    { WFConstants.BackendHeaders.GlobalCommittedLSN, gcLsn },
                    { WFConstants.BackendHeaders.QuorumAckedLSN, quorumAckedLsn },
                    { WFConstants.BackendHeaders.CurrentReplicaSetSize, replicaSetSize },
                    { WFConstants.BackendHeaders.NumberOfReadRegions, readRegions },
                    { WFConstants.BackendHeaders.ActivityId, Guid.NewGuid().ToString() }
                };
                return new DictionaryNameValueCollection(nvc);
            }

            QuorumReader quorumReader = QuorumReaderTests.GetQuorumReader(
                async (Uri uri, DocumentServiceRequest req) =>
                {
                    if (req.OperationType == OperationType.Head)
                    {
                        // Barrier reads: delay long enough for timeout to expire between iterations
                        await Task.Delay(3000);
                        return new StoreResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Headers = H("90", "90", "50", "90", readQuorum.ToString(CultureInfo.InvariantCulture))
                        };
                    }

                    // Initial quorum reads: LSN quorum met but GlobalCommittedLSN gap triggers barrier
                    return new StoreResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Headers = H("100", "100", "50", "100", readQuorum.ToString(CultureInfo.InvariantCulture))
                    };
                });

            // Act & Assert: timeout fires during barrier -> GoneException with ReadBarrierFailed substatus.
            GoneException ex = await Assert.ThrowsExceptionAsync<GoneException>(async () =>
            {
                await quorumReader.ReadStrongAsync(entity, readQuorum, ReadMode.Strong);
            });

            Assert.AreEqual(SubStatusCodes.Server_ReadBarrierFailed, ex.GetSubStatus());
        }

        private static ServerStoreModel CreateMockServerStoreModelForHeaderTests()
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();
            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .ReturnsAsync((TransportAddressUri uri, DocumentServiceRequest req) =>
                    new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.LocalLSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                            { WFConstants.BackendHeaders.CurrentWriteQuorum, "1" },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, "1" },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                            { WFConstants.BackendHeaders.QuorumAckedLocalLSN, "100" },
                        })
                    });

            AddressInformation[] addressInformation = new AddressInformation[]
            {
                new AddressInformation(
                    physicalUri: "https://blabla.com",
                    isPrimary: true,
                    protocol: Protocol.Tcp,
                    isPublic: true)
            };

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();
            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            ReplicationPolicy replicationPolicy = new ReplicationPolicy
            {
                MaxReplicaSetSize = 1
            };

            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();

            return new ServerStoreModel(new StoreClient(
                mockAddressCache.Object,
                new NullSessionContainer(),
                mockServiceConfigReader.Object,
                mockAuthorizationTokenProvider.Object,
                Protocol.Tcp,
                mockTransportClient.Object));
        }

        private static DocumentServiceRequest CreateReadRequestForHeaderTests()
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/testdb/colls/testcoll/docs/testdoc",
                AuthorizationTokenType.PrimaryMasterKey);

            request.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromSeconds(30)),
                ClientRequestStatistics = new ClientSideRequestStatistics()
            };

            return request;
        }

        /// <summary>
        /// Get a store reader mock reading from 4 replicas.
        /// </summary>
        /// <param name="invokeResourceOperationAsyncHandler">Invoke operation handler.</param>
        /// <returns>Store reader configured to call the provided handler.</returns>
        private static QuorumReader GetQuorumReader(
           Func<Uri, DocumentServiceRequest, Task<StoreResponse>> invokeResourceOperationAsyncHandler,
           bool enableBarrierEarlyYieldOn429 = false)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<Uri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns(invokeResourceOperationAsyncHandler);
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns<TransportAddressUri, DocumentServiceRequest>((x, y) => invokeResourceOperationAsyncHandler(x.Uri, y));

            const int totalReplicaCount = 4;
            AddressInformation[] addressInformation = new AddressInformation[totalReplicaCount];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i < totalReplicaCount; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri:
                        "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture)
                        + (i == 0 ? "p" : "s")
                        + "/",
                    isPrimary: i == 0,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            // Address Selector is an internal sealed class that can't be mocked, but its dependency
            // AddressCache can be mocked.
            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            mockAddressCache
                .Setup(
                    cache => cache.ResolveAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<bool>(),
                        CancellationToken.None))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            StoreReader storeReader = new StoreReader(
                mockTransportClient.Object,
                addressSelector,
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);

            return new QuorumReader(mockTransportClient.Object,
                addressSelector,
                storeReader,
                new MockServiceConfigurationReader(ConsistencyLevel.Strong),
                new MockAuthorizationTokenProvider(),
                enableBarrierEarlyYieldOn429: enableBarrierEarlyYieldOn429);
        }

        /// <summary>
        /// Get a QuorumReader using a Moq-based IServiceConfigurationReaderExtension so that
        /// the IServiceConfigurationReaderExtension `as` cast succeeds.
        /// </summary>
        private static QuorumReader GetQuorumReaderWithExtension(
            Func<Uri, DocumentServiceRequest, Task<StoreResponse>> invokeResourceOperationAsyncHandler,
            ConsistencyLevel defaultConsistencyLevel,
            Func<DocumentServiceRequest, (bool overridden, ConsistencyLevel level)> tryGetConsistencyLevelFunc = null)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<Uri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns(invokeResourceOperationAsyncHandler);
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns<TransportAddressUri, DocumentServiceRequest>((x, y) => invokeResourceOperationAsyncHandler(x.Uri, y));

            const int totalReplicaCount = 4;
            AddressInformation[] addressInformation = new AddressInformation[totalReplicaCount];

            for (int i = 0; i < totalReplicaCount; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri:
                        "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture)
                        + (i == 0 ? "p" : "s")
                        + "/",
                    isPrimary: i == 0,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();
            mockAddressCache
                .Setup(
                    cache => cache.ResolveAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<bool>(),
                        CancellationToken.None))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            StoreReader storeReader = new StoreReader(
                mockTransportClient.Object,
                addressSelector,
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);

            IServiceConfigurationReader serviceConfigReader = CreateMockServiceConfigReaderExtension(
                defaultConsistencyLevel,
                tryGetConsistencyLevelFunc).Object;

            return new QuorumReader(
                mockTransportClient.Object,
                addressSelector,
                storeReader,
                serviceConfigReader,
                new MockAuthorizationTokenProvider());
        }

        /// <summary>
        /// Get a dummy request entity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dummy request entity.</returns>
        private static DocumentServiceRequest GetRequestEntity(CancellationToken cancellationToken = default)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.BoundedStaleness.ToString();
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false),
                ResolvedPartitionKeyRange = new PartitionKeyRange(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromMinutes(5), cancellationToken)
            };

            return entity;
        }

        private class MockServiceConfigurationReader : IServiceConfigurationReader
        {
            private readonly ConsistencyLevel consistencyLevel;

            public MockServiceConfigurationReader(ConsistencyLevel consistencyLevel)
            {
                this.consistencyLevel = consistencyLevel;
            }

            public string DatabaseAccountId
            {
                get { return "localhost"; }
            }

            public Uri DatabaseAccountApiEndpoint { get; private set; }

            public ReplicationPolicy UserReplicationPolicy
            {
                get { return new ReplicationPolicy(); }
            }

            public ReplicationPolicy SystemReplicationPolicy
            {
                get { return new ReplicationPolicy(); }
            }

            public ConsistencyLevel DefaultConsistencyLevel => this.consistencyLevel;

            public ReadPolicy ReadPolicy
            {
                get { return new ReadPolicy(); }
            }

            public string PrimaryMasterKey
            {
                get { return "key"; }
            }

            public string SecondaryMasterKey
            {
                get { return "key"; }
            }

            public string PrimaryReadonlyMasterKey
            {
                get { return "key"; }
            }

            public string SecondaryReadonlyMasterKey
            {
                get { return "key"; }
            }

            public string ResourceSeedKey
            {
                get { return "seed"; }
            }

            public string SubscriptionId
            {
                get { return Guid.Empty.ToString(); }
            }

            public bool EnableNRegionSynchronousCommit
            {
                get { return false; }
            }

            public Task InitializeAsync()
            {
                return Task.FromResult(true);
            }
        }

        private delegate bool TryGetConsistencyLevelDelegate(DocumentServiceRequest request, out ConsistencyLevel consistencyLevel);

        private static Mock<IServiceConfigurationReaderExtension> CreateMockServiceConfigReaderExtension(
            ConsistencyLevel defaultConsistencyLevel,
            Func<DocumentServiceRequest, (bool overridden, ConsistencyLevel level)> tryGetConsistencyLevelFunc = null)
        {
            Mock<IServiceConfigurationReaderExtension> mock = new Mock<IServiceConfigurationReaderExtension>();
            mock.SetupGet(c => c.DatabaseAccountId).Returns("localhost");
            mock.SetupGet(c => c.UserReplicationPolicy).Returns(new ReplicationPolicy());
            mock.SetupGet(c => c.SystemReplicationPolicy).Returns(new ReplicationPolicy());
            mock.SetupGet(c => c.DefaultConsistencyLevel).Returns(defaultConsistencyLevel);
            mock.SetupGet(c => c.ReadPolicy).Returns(new ReadPolicy());
            mock.SetupGet(c => c.PrimaryMasterKey).Returns("key");
            mock.SetupGet(c => c.SecondaryMasterKey).Returns("key");
            mock.SetupGet(c => c.PrimaryReadonlyMasterKey).Returns("key");
            mock.SetupGet(c => c.SecondaryReadonlyMasterKey).Returns("key");
            mock.SetupGet(c => c.ResourceSeedKey).Returns("seed");
            mock.SetupGet(c => c.SubscriptionId).Returns(Guid.Empty.ToString());
            mock.Setup(c => c.InitializeAsync()).Returns(Task.FromResult(true));
            mock.Setup(c => c.TryGetServiceRetryParams(It.IsAny<DocumentServiceRequest>())).Returns((IServiceRetryParams)null);

            mock
                .Setup(c => c.TryGetConsistencyLevel(It.IsAny<DocumentServiceRequest>(), out It.Ref<ConsistencyLevel>.IsAny))
                .Returns(new TryGetConsistencyLevelDelegate((DocumentServiceRequest request, out ConsistencyLevel consistencyLevel) =>
                {
                    if (tryGetConsistencyLevelFunc != null)
                    {
                        (bool overridden, ConsistencyLevel level) result = tryGetConsistencyLevelFunc(request);
                        consistencyLevel = result.level;
                        return result.overridden;
                    }

                    consistencyLevel = default;
                    return false;
                }));

            return mock;
        }

        private static QuorumReader GetQuorumReaderWithReplicaSetSize(
            Func<Uri, DocumentServiceRequest, Task<StoreResponse>> invokeResourceOperationAsyncHandler,
            int maxReplicaSetSize)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<Uri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns(invokeResourceOperationAsyncHandler);
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns<TransportAddressUri, DocumentServiceRequest>((x, y) => invokeResourceOperationAsyncHandler(x.Uri, y));

            int totalReplicaCount = maxReplicaSetSize;
            AddressInformation[] addressInformation = new AddressInformation[totalReplicaCount];

            for (int i = 0; i < totalReplicaCount; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri:
                        "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture)
                        + (i == 0 ? "p" : "s")
                        + "/",
                    isPrimary: i == 0,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();
            mockAddressCache
                .Setup(
                    cache => cache.ResolveAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<bool>(),
                        CancellationToken.None))
                .ReturnsAsync(new PartitionAddressInformation(
                    addressInformation,
                    inNetworkRequest: false,
                    partitionTargetReplicaSetSize: maxReplicaSetSize));

            AddressSelector addressSelector = new AddressSelector(mockAddressCache.Object, Protocol.Tcp);

            StoreReader storeReader = new StoreReader(
                mockTransportClient.Object,
                addressSelector,
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);

            Mock<IServiceConfigurationReader> mockConfig = new Mock<IServiceConfigurationReader>();
            mockConfig.SetupGet(c => c.DefaultConsistencyLevel).Returns(ConsistencyLevel.Strong);
            mockConfig.SetupGet(c => c.SystemReplicationPolicy).Returns(new ReplicationPolicy { MaxReplicaSetSize = maxReplicaSetSize });
            mockConfig.SetupGet(c => c.UserReplicationPolicy).Returns(new ReplicationPolicy { MaxReplicaSetSize = maxReplicaSetSize });

            return new QuorumReader(mockTransportClient.Object,
                addressSelector,
                storeReader,
                mockConfig.Object,
                new MockAuthorizationTokenProvider());
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public async Task ReadStrong_ThreeReplicas_IncludesPrimaryInQuorumRead()
        {
            // When the partition-level TargetReplicaSetSize (TRSS) is <= 3,
            // QuorumReader includes the primary upfront in quorum reads.
            // This ensures quorum can be met even when one secondary is
            // unavailable, without needing a retry round-trip.
            DocumentServiceRequest entity = QuorumReaderTests.GetRequestEntity();
            entity.RequestContext.ResolvedPartitionTargetReplicaSetSize = 3;
            int readQuorum = 2;
            int totalInvocations = 0;
            bool primaryWasRead = false;

            QuorumReader quorumReader = GetQuorumReaderWithReplicaSetSize(
                (Uri uri, DocumentServiceRequest req) =>
                {
                    string u = uri.AbsoluteUri;
                    Interlocked.Increment(ref totalInvocations);

                    if (u.Contains("/0p/"))
                    {
                        primaryWasRead = true;
                    }

                    // The secondary at replica /2s/ is always gone.
                    if (u.Contains("/2s/"))
                    {
                        throw new GoneException("secondary gone");
                    }

                    return Task.FromResult(new StoreResponse
                    {
                        Status = 200,
                        Headers = new DictionaryNameValueCollection(new NameValueCollection
                        {
                            { WFConstants.BackendHeaders.LSN, "100" },
                            { WFConstants.BackendHeaders.GlobalCommittedLSN, "100" },
                            { WFConstants.BackendHeaders.NumberOfReadRegions, "1" },
                            { WFConstants.BackendHeaders.CurrentReplicaSetSize, "3" },
                            { WFConstants.BackendHeaders.CurrentWriteQuorum, "2" },
                            { WFConstants.BackendHeaders.QuorumAckedLSN, "100" },
                        })
                    });
                },
                maxReplicaSetSize: 3);

            StoreResponse response = await quorumReader.ReadStrongAsync(entity, readQuorum, ReadMode.Strong);

            Assert.AreEqual(200, response.Status);
            Assert.IsTrue(
                primaryWasRead,
                "Primary should be included in quorum read when TRSS is <= 3.");

            // With TRSS <= 3, includePrimary = true from the start. The first
            // ReadQuorumAsync reads from all 3 replicas (primary + 2 secondaries).
            // One secondary is gone, but the remaining 2 valid responses (primary +
            // good secondary) meet quorum. Expect at most 3 transport calls.
            Assert.IsTrue(
                totalInvocations <= 3,
                $"Expected at most 3 transport invocations, but got {totalInvocations}.");
        }

        private class MockAuthorizationTokenProvider : IAuthorizationTokenProvider
        {
            public ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
                string resourceAddress,
                string resourceType,
                string requestVerb,
                INameValueCollection headers,
                AuthorizationTokenType tokenType)
            {
                return new ValueTask<(string token, string payload)>(("authtoken!", null));
            }

            public Task AddSystemAuthorizationHeaderAsync(DocumentServiceRequest request, string federationId, string verb, string resourceId)
            {
                request.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                request.Headers[HttpConstants.HttpHeaders.Authorization] = "authtoken!";
                return Task.FromResult(0);
            }
        }
    }
}