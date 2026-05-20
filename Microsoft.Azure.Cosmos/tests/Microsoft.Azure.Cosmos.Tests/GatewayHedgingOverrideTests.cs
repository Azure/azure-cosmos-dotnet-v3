namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Unit tests for the Gateway-controlled <c>disableCrossRegionalHedging</c> flag on
    /// <see cref="AccountProperties"/> and the resulting hedging strategy reconciliation in
    /// <see cref="DocumentClient"/>.
    /// </summary>
    [TestClass]
    public class GatewayHedgingOverrideTests
    {
        private const string AccountEndpoint = "https://localhost:8081/";
        private const string AccountKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string HedgingDisabledByGatewayDiagnosticsKey = TraceDatumKeys.HedgingDisabledByGateway;

        [TestMethod]
        public void AccountProperties_DisableCrossRegionalHedging_True_Deserializes()
        {
            string json = "{\"id\":\"test\",\"disableCrossRegionalHedging\":true}";
            AccountProperties props = JsonConvert.DeserializeObject<AccountProperties>(json);
            Assert.IsTrue(props.DisableCrossRegionalHedging.HasValue);
            Assert.IsTrue(props.DisableCrossRegionalHedging.Value);
        }

        [TestMethod]
        public void AccountProperties_DisableCrossRegionalHedging_False_Deserializes()
        {
            string json = "{\"id\":\"test\",\"disableCrossRegionalHedging\":false}";
            AccountProperties props = JsonConvert.DeserializeObject<AccountProperties>(json);
            Assert.IsTrue(props.DisableCrossRegionalHedging.HasValue);
            Assert.IsFalse(props.DisableCrossRegionalHedging.Value);
        }

        [TestMethod]
        public void AccountProperties_DisableCrossRegionalHedging_Absent_DefaultsToNull()
        {
            string json = "{\"id\":\"test\"}";
            AccountProperties props = JsonConvert.DeserializeObject<AccountProperties>(json);
            Assert.IsFalse(props.DisableCrossRegionalHedging.HasValue);
        }

        [TestMethod]
        public void AccountProperties_DisableCrossRegionalHedging_Unknown_FieldDoesNotBreakDeserialization()
        {
            string json = "{\"id\":\"test\",\"disableCrossRegionalHedging\":true,\"someFutureFlag\":\"foo\"}";
            AccountProperties props = JsonConvert.DeserializeObject<AccountProperties>(json);
            Assert.IsTrue(props.DisableCrossRegionalHedging.GetValueOrDefault());
        }

        [TestMethod]
        public void InitializePartitionLevelFailoverWithDefaultHedging_FlagTrue_SkipsApplyingDefaultStrategy()
        {
            DocumentClient client = CreateClient(new ConnectionPolicy { EnablePartitionLevelFailover = true });
            try
            {
                Assert.IsNull(client.ConnectionPolicy.AvailabilityStrategy, "Pre-condition: no strategy configured");

                SetDisableHedgingFlag(client, true);
                client.InitializePartitionLevelFailoverWithDefaultHedging();

                Assert.IsNull(
                    client.ConnectionPolicy.AvailabilityStrategy,
                    "Default hedging must NOT be applied when disableCrossRegionalHedging=true");
            }
            finally
            {
                client.Dispose();
            }
        }

        [TestMethod]
        public void InitializePartitionLevelFailoverWithDefaultHedging_FlagFalse_AppliesDefaultStrategy()
        {
            DocumentClient client = CreateClient(new ConnectionPolicy { EnablePartitionLevelFailover = true });
            try
            {
                SetDisableHedgingFlag(client, false);
                client.InitializePartitionLevelFailoverWithDefaultHedging();

                Assert.IsNotNull(
                    client.ConnectionPolicy.AvailabilityStrategy,
                    "Default PPAF hedging must be applied when disableCrossRegionalHedging=false");
            }
            finally
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// Regression test for the init-path race called out on PR #5829: in production,
        /// <see cref="DocumentClient.InitializePartitionLevelFailoverWithDefaultHedging"/> is invoked
        /// from <c>DocumentClient.cs</c> line 1108 — i.e. <em>after</em>
        /// <c>InitializeGatewayConfigurationReaderAsync</c> subscribes the
        /// <see cref="Routing.GlobalEndpointManager.OnEnablePartitionLevelFailoverConfigChanged"/>
        /// handler and starts the background account-properties refresh loop. A refresh that fires in
        /// that narrow window can flip <c>disableCrossRegionalHedging</c> via
        /// <see cref="DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(bool, bool)"/>
        /// before the init thread reaches the default-hedging block. Without acquire/release semantics
        /// on the flag field, the init-path read could observe a stale value and silently install the
        /// SDK default hedging strategy <em>after</em> the operator just disabled hedging.
        ///
        /// Companion to <see cref="InitializePartitionLevelFailoverWithDefaultHedging_FlagTrue_SkipsApplyingDefaultStrategy"/>:
        /// that test pins the flag through the synthetic test setter; this one exercises the actual
        /// refresh code path that the race scenario depends on.
        /// </summary>
        [TestMethod]
        public void InitializePartitionLevelFailoverWithDefaultHedging_AfterRefreshDrivenFlagFlip_SkipsApplyingDefaultStrategy()
        {
            DocumentClient client = CreateClient(new ConnectionPolicy { EnablePartitionLevelFailover = true });
            try
            {
                Assert.IsNull(client.ConnectionPolicy.AvailabilityStrategy, "Pre-condition: no strategy configured");
                Assert.IsFalse(client.DisableCrossRegionalHedgingForTests, "Pre-condition: flag starts at default false");

                // Simulate a refresh-driven flag flip — this is exactly what GEM's background-refresh
                // loop calls when AccountProperties.DisableCrossRegionalHedging transitions, and it is
                // the write that the init-path read at DocumentClient.cs:6963 races against.
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                    latestIsEnabled: true,
                    latestDisableCrossRegionalHedging: true);

                Assert.IsTrue(
                    client.DisableCrossRegionalHedgingForTests,
                    "Refresh callback must have published the flag transition");

                // Now exercise the init-path read. With volatile semantics on the field, the read here
                // has acquire semantics and is guaranteed to observe the published value above — and
                // therefore must skip applying the SDK default hedging strategy.
                client.InitializePartitionLevelFailoverWithDefaultHedging();

                Assert.IsNull(
                    client.ConnectionPolicy.AvailabilityStrategy,
                    "Init-path read of disableCrossRegionalHedging must observe the post-refresh value " +
                    "and skip applying the SDK default hedging strategy");
            }
            finally
            {
                client.Dispose();
            }
        }

        [TestMethod]
        public void UpdateConfig_FlagToggleOn_StashesCustomerStrategyAndClearsAvailabilityStrategy()
        {
            ConnectionPolicy policy = new ConnectionPolicy { EnablePartitionLevelFailover = true };
            CrossRegionHedgingAvailabilityStrategy customerStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(500),
                thresholdStep: TimeSpan.FromMilliseconds(100));
            policy.AvailabilityStrategy = customerStrategy;

            DocumentClient client = CreateClient(policy);
            try
            {
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: true);

                Assert.IsNull(client.ConnectionPolicy.AvailabilityStrategy, "Strategy must be cleared when disable flag flips to true");
                Assert.AreSame(
                    customerStrategy,
                    GetCustomerStashedStrategy(client),
                    "Customer-configured strategy must be stashed for later restoration");
            }
            finally
            {
                client.Dispose();
            }
        }

        [TestMethod]
        public void UpdateConfig_FlagToggleOff_RestoresStashedCustomerStrategy()
        {
            ConnectionPolicy policy = new ConnectionPolicy { EnablePartitionLevelFailover = true };
            CrossRegionHedgingAvailabilityStrategy customerStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(500),
                thresholdStep: TimeSpan.FromMilliseconds(100));
            policy.AvailabilityStrategy = customerStrategy;

            DocumentClient client = CreateClient(policy);
            try
            {
                // Step 1: disable hedging — strategy is stashed.
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: true);
                Assert.IsNull(client.ConnectionPolicy.AvailabilityStrategy);

                // Step 2: re-enable hedging — strategy is restored.
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: false);

                Assert.AreSame(
                    customerStrategy,
                    client.ConnectionPolicy.AvailabilityStrategy,
                    "Customer-configured strategy must be restored when the disable flag toggles back to false");
                Assert.IsNull(GetCustomerStashedStrategy(client), "Stash must be cleared after restoration");
            }
            finally
            {
                client.Dispose();
            }
        }

        [TestMethod]
        public void UpdateConfig_ClientLevelOverrideDisabled_FlagIsIgnored()
        {
            ConnectionPolicy policy = new ConnectionPolicy
            {
                EnablePartitionLevelFailover = true,
                DisablePartitionLevelFailoverClientLevelOverride = true,
            };
            CrossRegionHedgingAvailabilityStrategy customerStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(500),
                thresholdStep: TimeSpan.FromMilliseconds(100));
            policy.AvailabilityStrategy = customerStrategy;

            DocumentClient client = CreateClient(policy);
            try
            {
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: true);

                Assert.AreSame(
                    customerStrategy,
                    client.ConnectionPolicy.AvailabilityStrategy,
                    "When DisablePartitionLevelFailoverClientLevelOverride=true, the gateway flag must be ignored entirely");
            }
            finally
            {
                client.Dispose();
            }
        }

        [TestMethod]
        public void UpdateConfig_FlagTrue_DoesNotStashSDKDefaultStrategy()
        {
            ConnectionPolicy policy = new ConnectionPolicy { EnablePartitionLevelFailover = true };
            DocumentClient client = CreateClient(policy);
            try
            {
                // Apply SDK default first.
                client.InitializePartitionLevelFailoverWithDefaultHedging();
                Assert.IsNotNull(client.ConnectionPolicy.AvailabilityStrategy);
                Assert.IsTrue(((CrossRegionHedgingAvailabilityStrategy)client.ConnectionPolicy.AvailabilityStrategy).IsSDKDefaultStrategyForPPAF);

                // Now flip the flag — the SDK default should be cleared but NOT stashed.
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: true);

                Assert.IsNull(client.ConnectionPolicy.AvailabilityStrategy);
                Assert.IsNull(
                    GetCustomerStashedStrategy(client),
                    "SDK-default strategy must NOT be stashed — it can be regenerated deterministically");

                // Toggle back off — SDK default is rebuilt from PPAF state.
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: false);

                Assert.IsNotNull(client.ConnectionPolicy.AvailabilityStrategy);
                Assert.IsTrue(((CrossRegionHedgingAvailabilityStrategy)client.ConnectionPolicy.AvailabilityStrategy).IsSDKDefaultStrategyForPPAF);
            }
            finally
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// Regression test: when the Gateway flag is <c>true</c> on the underlying
        /// <see cref="DocumentClient"/>, <see cref="RequestInvokerHandler.AvailabilityStrategy(RequestMessage)"/>
        /// MUST return <c>null</c> even if the request itself supplies a per-request
        /// <see cref="AvailabilityStrategy"/> via <see cref="RequestOptions.AvailabilityStrategy"/>.
        /// This is the absolute-precedence guarantee called out in the spec — the operator's
        /// kill-switch wins over both per-request and client-level configuration.
        /// </summary>
        [TestMethod]
        public void RequestInvokerHandler_FlagTrue_PerRequestStrategy_ReturnsNull()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.DisableCrossRegionalHedgingForTests = true;

            RequestInvokerHandler handler = new RequestInvokerHandler(
                mockCosmosClient,
                requestedClientConsistencyLevel: null,
                requestedClientReadConsistencyStrategy: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null);

            using RequestMessage request = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                RequestOptions = new RequestOptions
                {
                    AvailabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(500),
                        thresholdStep: TimeSpan.FromMilliseconds(100))
                }
            };

            AvailabilityStrategyInternal resolved = handler.AvailabilityStrategy(request);

            Assert.IsNull(
                resolved,
                "Gateway operator override must take absolute precedence over per-request AvailabilityStrategy");
        }

        /// <summary>
        /// Companion to <see cref="RequestInvokerHandler_FlagTrue_PerRequestStrategy_ReturnsNull"/>:
        /// when the Gateway flag is <c>false</c>, the per-request <see cref="AvailabilityStrategy"/>
        /// MUST be honored — the override only suppresses hedging while the flag is true.
        /// </summary>
        [TestMethod]
        public void RequestInvokerHandler_FlagFalse_HonorsPerRequestStrategy()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.DisableCrossRegionalHedgingForTests = false;

            RequestInvokerHandler handler = new RequestInvokerHandler(
                mockCosmosClient,
                requestedClientConsistencyLevel: null,
                requestedClientReadConsistencyStrategy: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null);

            CrossRegionHedgingAvailabilityStrategy perRequestStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(500),
                thresholdStep: TimeSpan.FromMilliseconds(100));

            using RequestMessage request = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                RequestOptions = new RequestOptions
                {
                    AvailabilityStrategy = perRequestStrategy
                }
            };

            AvailabilityStrategyInternal resolved = handler.AvailabilityStrategy(request);

            Assert.IsNotNull(resolved, "Per-request strategy must be honored when Gateway flag is false");
            Assert.AreSame(
                perRequestStrategy,
                resolved,
                "Per-request strategy must be returned verbatim when Gateway flag is false");
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrue_FirstSuppressedRequest_EmitsDiagnosticsOnce()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy = CreateCustomerHedgingStrategy();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            using ResponseMessage firstResponse = await SendReadRequestAsync(handler);
            AssertHedgingDisabledByGatewayDiagnostic(firstResponse);

            using ResponseMessage secondResponse = await SendReadRequestAsync(handler);
            AssertNoHedgingDisabledByGatewayDiagnostic(secondResponse);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagFalse_DoesNotEmitDiagnostics()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: false);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            using ResponseMessage response = await SendReadRequestAsync(handler);
            AssertNoHedgingDisabledByGatewayDiagnostic(response);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrue_NoEffectiveStrategy_DoesNotConsumeDiagnosticsMarker()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            using ResponseMessage noStrategyResponse = await SendReadRequestAsync(handler);
            AssertNoHedgingDisabledByGatewayDiagnostic(noStrategyResponse);

            using ResponseMessage enabledStrategyResponse = await SendReadRequestAsync(
                handler,
                CreateCustomerHedgingStrategy());
            AssertHedgingDisabledByGatewayDiagnostic(enabledStrategyResponse);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrue_PerRequestDisabledStrategy_DoesNotConsumeDiagnosticsMarker()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy = CreateCustomerHedgingStrategy();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            using ResponseMessage disabledStrategyResponse = await SendReadRequestAsync(
                handler,
                AvailabilityStrategy.DisabledStrategy());
            AssertNoHedgingDisabledByGatewayDiagnostic(disabledStrategyResponse);

            using ResponseMessage enabledStrategyResponse = await SendReadRequestAsync(
                handler,
                CreateCustomerHedgingStrategy());
            AssertHedgingDisabledByGatewayDiagnostic(enabledStrategyResponse);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrue_DocumentCreate_DoesNotConsumeDiagnosticsMarker()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy = CreateCustomerHedgingStrategy();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            using ResponseMessage createResponse = await SendRequestAsync(
                handler,
                ResourceType.Document,
                OperationType.Create);
            AssertNoHedgingDisabledByGatewayDiagnostic(createResponse);

            using ResponseMessage readResponse = await SendReadRequestAsync(handler);
            AssertHedgingDisabledByGatewayDiagnostic(readResponse);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrue_NonDocumentRead_DoesNotConsumeDiagnosticsMarker()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy = CreateCustomerHedgingStrategy();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            using ResponseMessage databaseReadResponse = await SendRequestAsync(
                handler,
                ResourceType.Database,
                OperationType.Read);
            AssertNoHedgingDisabledByGatewayDiagnostic(databaseReadResponse);

            using ResponseMessage documentReadResponse = await SendReadRequestAsync(handler);
            AssertHedgingDisabledByGatewayDiagnostic(documentReadResponse);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrueFalseTrue_ReEmitsDiagnosticsAfterFlagCycle()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy = CreateCustomerHedgingStrategy();
            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);

            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            using ResponseMessage firstTrueResponse = await SendReadRequestAsync(handler);
            AssertHedgingDisabledByGatewayDiagnostic(firstTrueResponse);

            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: false);

            using ResponseMessage falseResponse = await SendReadRequestAsync(handler);
            AssertNoHedgingDisabledByGatewayDiagnostic(falseResponse);

            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            using ResponseMessage secondTrueResponse = await SendReadRequestAsync(handler);
            AssertHedgingDisabledByGatewayDiagnostic(secondTrueResponse);
        }

        [TestMethod]
        public async Task RequestInvokerHandler_FlagTrue_ConcurrentFirstSuppressedRequests_EmitsDiagnosticsOnce()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy = CreateCustomerHedgingStrategy();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            TestRequestInvokerHandler handler = new TestRequestInvokerHandler(mockCosmosClient);
            using ManualResetEventSlim startGate = new ManualResetEventSlim(false);
            Task<bool>[] requests = Enumerable
                .Range(0, 16)
                .Select(_ => Task.Run(() =>
                {
                    startGate.Wait();

                    using ITrace trace = Trace.GetRootTrace("Gateway hedging diagnostics concurrency test");
                    using RequestMessage request = CreateReadRequest(trace);

                    AvailabilityStrategyInternal resolved = handler.AvailabilityStrategy(request);
                    Assert.IsNull(
                        resolved,
                        "Gateway operator override must suppress hedging for every concurrent first request");

                    return TryGetBooleanTraceDatumValue(trace, HedgingDisabledByGatewayDiagnosticsKey, out bool value)
                        && value;
                }))
                .ToArray();

            startGate.Set();
            bool[] emittedDiagnostics = await Task.WhenAll(requests);

            Assert.AreEqual(
                1,
                emittedDiagnostics.Count(emitted => emitted),
                "Exactly one concurrent first suppressed request should consume the one-shot diagnostics marker");
        }

        [TestMethod]
        public void DocumentClient_ObservedSuppressionThenFlagFalse_DiagnosticMarkerStillConsumable()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            Assert.IsTrue(
                mockCosmosClient.DocumentClient.IsHedgingDisabledByGateway,
                "Pre-condition: request path observed gateway suppression as active");
            Assert.IsTrue(
                mockCosmosClient.DocumentClient.TryGetHedgingDisabledByGatewayDiagnosticGeneration(out long generation),
                "Pre-condition: request path captured the active diagnostics generation");

            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: false);

            Assert.IsFalse(
                mockCosmosClient.DocumentClient.IsHedgingDisabledByGateway,
                "False transition should unpublish suppression for later requests");

            Assert.IsTrue(
                mockCosmosClient.DocumentClient.TryConsumeHedgingDisabledByGatewayDiagnosticForSuppressedRequest(generation),
                "A request that already observed suppression before the false transition should still emit diagnostics");

            Assert.IsFalse(
                mockCosmosClient.DocumentClient.TryConsumeHedgingDisabledByGatewayDiagnosticForSuppressedRequest(generation),
                "The diagnostics marker must remain one-shot after the racing suppressed request consumes it");
        }

        [TestMethod]
        public void DocumentClient_ObservedSuppressionThenFlagFalseTrue_DoesNotConsumeNewCycleMarker()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            Assert.IsTrue(
                mockCosmosClient.DocumentClient.TryGetHedgingDisabledByGatewayDiagnosticGeneration(out long firstGeneration),
                "Pre-condition: stale request captured the first active diagnostics generation");

            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: false);
            mockCosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: false,
                latestDisableCrossRegionalHedging: true);

            Assert.IsTrue(
                mockCosmosClient.DocumentClient.TryGetHedgingDisabledByGatewayDiagnosticGeneration(out long secondGeneration),
                "Pre-condition: later request captured the second active diagnostics generation");
            Assert.AreNotEqual(
                firstGeneration,
                secondGeneration,
                "A false -> true cycle must publish a distinct diagnostics generation");

            Assert.IsTrue(
                mockCosmosClient.DocumentClient.TryConsumeHedgingDisabledByGatewayDiagnosticForSuppressedRequest(firstGeneration),
                "A stale request can still consume the marker for the generation it observed");

            Assert.IsTrue(
                mockCosmosClient.DocumentClient.TryConsumeHedgingDisabledByGatewayDiagnosticForSuppressedRequest(secondGeneration),
                "The stale request must not consume the marker for the later true cycle");

            Assert.IsFalse(
                mockCosmosClient.DocumentClient.TryConsumeHedgingDisabledByGatewayDiagnosticForSuppressedRequest(secondGeneration),
                "The later true-cycle diagnostics marker must remain one-shot");
        }

        private static DocumentClient CreateClient(ConnectionPolicy policy)
        {
            return new DocumentClient(new Uri(AccountEndpoint), AccountKey, policy);
        }

        private static void SetDisableHedgingFlag(DocumentClient client, bool value)
        {
            client.DisableCrossRegionalHedgingForTests = value;
        }

        private static AvailabilityStrategy GetCustomerStashedStrategy(DocumentClient client)
        {
            return client.CustomerConfiguredAvailabilityStrategyForTests;
        }

        private static CrossRegionHedgingAvailabilityStrategy CreateCustomerHedgingStrategy()
        {
            return new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(500),
                thresholdStep: TimeSpan.FromMilliseconds(100));
        }

        private static async Task<ResponseMessage> SendReadRequestAsync(
            RequestInvokerHandler handler,
            AvailabilityStrategy availabilityStrategy = null)
        {
            return await SendRequestAsync(
                handler,
                ResourceType.Document,
                OperationType.Read,
                availabilityStrategy);
        }

        private static async Task<ResponseMessage> SendRequestAsync(
            RequestInvokerHandler handler,
            ResourceType resourceType,
            OperationType operationType,
            AvailabilityStrategy availabilityStrategy = null)
        {
            ITrace trace = Trace.GetRootTrace("Gateway hedging diagnostics test");
            RequestMessage request = CreateRequest(trace, resourceType, operationType);
            request.RequestOptions.AvailabilityStrategy = availabilityStrategy;
            return await handler.SendAsync(request, CancellationToken.None);
        }

        private static RequestMessage CreateReadRequest(ITrace trace)
        {
            return CreateRequest(trace, ResourceType.Document, OperationType.Read);
        }

        private static RequestMessage CreateRequest(
            ITrace trace,
            ResourceType resourceType,
            OperationType operationType)
        {
            RequestMessage request = new RequestMessage(
                operationType == OperationType.Create ? HttpMethod.Post : HttpMethod.Get,
                GetResourceLink(resourceType),
                trace)
            {
                ResourceType = resourceType,
                OperationType = operationType,
                RequestOptions = new RequestOptions()
            };

            if (resourceType == ResourceType.Document)
            {
                request.Headers.PartitionKey = "[\"testPk\"]";
            }

            return request;
        }

        private static string GetResourceLink(ResourceType resourceType)
        {
            return resourceType == ResourceType.Database
                ? "/dbs/testdb"
                : "/dbs/testdb/colls/testcontainer/docs/testId";
        }

        private static void AssertHedgingDisabledByGatewayDiagnostic(ResponseMessage response)
        {
            Assert.IsTrue(
                TryGetHedgingDisabledByGatewayDiagnostic(response, out object value),
                $"Expected {HedgingDisabledByGatewayDiagnosticsKey} to be present in CosmosDiagnostics");
            Assert.IsInstanceOfType(
                value,
                typeof(BooleanTraceDatum),
                $"Expected {HedgingDisabledByGatewayDiagnosticsKey} to be represented as a typed boolean trace datum");
            Assert.IsTrue(((BooleanTraceDatum)value).Value);
            StringAssert.Contains(
                response.Diagnostics.ToString(),
                $"\"{HedgingDisabledByGatewayDiagnosticsKey}\":true");
        }

        private static void AssertNoHedgingDisabledByGatewayDiagnostic(ResponseMessage response)
        {
            Assert.IsFalse(
                TryGetHedgingDisabledByGatewayDiagnostic(response, out _),
                $"Did not expect {HedgingDisabledByGatewayDiagnosticsKey} to be present in CosmosDiagnostics");
            Assert.IsFalse(
                response.Diagnostics.ToString().Contains(HedgingDisabledByGatewayDiagnosticsKey),
                $"Did not expect {HedgingDisabledByGatewayDiagnosticsKey} to be present in CosmosDiagnostics text");
        }

        private static bool TryGetHedgingDisabledByGatewayDiagnostic(ResponseMessage response, out object value)
        {
            CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)response.Diagnostics;
            return diagnostics.Value.TryGetDatum(HedgingDisabledByGatewayDiagnosticsKey, out value);
        }

        private static bool TryGetBooleanTraceDatumValue(ITrace trace, string key, out bool value)
        {
            value = false;
            if (!trace.TryGetDatum(key, out object datum)
                || datum is not BooleanTraceDatum booleanTraceDatum)
            {
                return false;
            }

            value = booleanTraceDatum.Value;
            return true;
        }

        private sealed class TestRequestInvokerHandler : RequestInvokerHandler
        {
            public TestRequestInvokerHandler(CosmosClient client)
                : base(
                    client,
                    requestedClientConsistencyLevel: null,
                    requestedClientReadConsistencyStrategy: null,
                    requestedClientPriorityLevel: null,
                    requestedClientThroughputBucket: null)
            {
            }

            public override Task<ResponseMessage> BaseSendAsync(
                RequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new ResponseMessage(HttpStatusCode.OK, request));
            }
        }
    }
}
