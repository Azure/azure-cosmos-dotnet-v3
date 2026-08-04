namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
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

        /// <summary>
        /// Pins the exact JSON property name used on the wire. The key <c>disableCrossRegionalHedging</c>
        /// is the contract with the Gateway; renaming it (even with a lockstep update to the ordering-only
        /// serializer tests) would silently break wire-binding. This serialize round-trip fails loudly if
        /// the key ever changes without a coordinated server-side rename.
        /// </summary>
        [TestMethod]
        public void DisableCrossRegionalHedging_SerializesWithExactJsonKey()
        {
            AccountProperties props = new AccountProperties { DisableCrossRegionalHedging = true };
            string json = JsonConvert.SerializeObject(props);
            Assert.IsTrue(
                json.Contains("\"disableCrossRegionalHedging\":true"),
                "JSON property name is the wire contract with Gateway and must not change without a coordinated server-side rename.");
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
                "Gateway operator override (set via the test-only setter) must take absolute precedence " +
                "over per-request AvailabilityStrategy — this validates the handler short-circuit in isolation");
        }

        /// <summary>
        /// Companion to <see cref="RequestInvokerHandler_FlagTrue_PerRequestStrategy_ReturnsNull"/> that
        /// drives the flag through the <em>production</em> reconcile path
        /// (<see cref="DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(bool, bool)"/>)
        /// rather than the test-only setter, then asserts the per-request precedence end-to-end:
        /// <see cref="RequestInvokerHandler.AvailabilityStrategy(RequestMessage)"/> MUST return <c>null</c>
        /// even with a per-request strategy supplied. Keeping <c>latestIsEnabled == current</c> avoids the
        /// PPAF-enablement path (<c>SetIsPPAFEnabled</c>) that would NRE on a never-opened mock client.
        /// </summary>
        [TestMethod]
        public void RequestInvokerHandler_FlagDrivenByRefreshCallback_PerRequestStrategy_ReturnsNull()
        {
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            DocumentClient dc = mockCosmosClient.DocumentClient;
            dc.ConnectionPolicy.EnablePartitionLevelFailover = true;
            dc.ConnectionPolicy.AvailabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(500),
                thresholdStep: TimeSpan.FromMilliseconds(100));

            // Drive the flag through the REAL production callback. latestIsEnabled == current
            // (EnablePartitionLevelFailover already true) so ppafEnablementChanged is false and we avoid the
            // SetIsPPAFEnabled path; hedgingFlagChanged is true so the reconcile stashes + clears the strategy.
            dc.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: true,
                latestDisableCrossRegionalHedging: true);

            Assert.IsTrue(dc.DisableCrossRegionalHedgingForTests, "Refresh callback must have published the flag");

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
                "Gateway override driven through the production reconcile path must suppress per-request hedging");
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

        /// <summary>
        /// M1 coverage for the subscriber-side PPAF revert. When the hedging-strategy reconcile throws
        /// while a PPAF-enablement transition is being applied,
        /// <see cref="DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(bool, bool)"/>
        /// must revert the applied connection-policy flags (and the
        /// <see cref="DocumentClient.PartitionKeyRangeLocation"/> PPAF/PPCB state) so the top-of-method
        /// no-op guard does not swallow the GlobalEndpointManager re-fire. A second call with the same
        /// <c>latestIsEnabled</c> must therefore re-apply the transition end-to-end rather than returning
        /// early — the exact "transient failure goes permanently silent" mode this PR exists to prevent.
        /// </summary>
        [TestMethod]
        public void UpdateConfig_PpafEnablement_ReconcileThrows_RevertsStateSoRetryIsNotNoOp()
        {
            ConnectionPolicy policy = new ConnectionPolicy { EnablePartitionLevelFailover = false };
            DocumentClient client = CreateClient(policy);
            try
            {
                // Inject a stub PartitionKeyRangeLocation so the PPAF path (SetIsPPAFEnabled /
                // SetIsPPCBEnabled) runs without a fully-opened client.
                Mock<GlobalPartitionEndpointManager> partitionKeyRangeLocation = new Mock<GlobalPartitionEndpointManager>();
                client.PartitionKeyRangeLocationForTests = partitionKeyRangeLocation.Object;

                // Force the reconcile to throw on the first (PPAF false -> true) transition, after the
                // applied-state mutations have already committed inside the try.
                client.ReconcileFailureHookForTests = () => throw new InvalidOperationException("Simulated reconcile failure");

                Assert.ThrowsException<InvalidOperationException>(
                    () => client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: false),
                    "The forced reconcile failure must propagate to the caller (GEM), which rolls back its own baseline in tandem.");

                Assert.IsFalse(
                    client.ConnectionPolicy.EnablePartitionLevelFailover,
                    "EnablePartitionLevelFailover must be reverted after a throwing reconcile so the transition stays re-detectable.");
                Assert.IsFalse(
                    client.ConnectionPolicy.EnablePartitionLevelCircuitBreaker,
                    "EnablePartitionLevelCircuitBreaker must be reverted in tandem with the PPAF flag.");

                partitionKeyRangeLocation.Verify(
                    p => p.SetIsPPAFEnabled(false),
                    Times.AtLeastOnce(),
                    "Revert must restore the PartitionKeyRangeLocation PPAF state to its pre-change value.");
                partitionKeyRangeLocation.Verify(
                    p => p.SetIsPPCBEnabled(false),
                    Times.AtLeastOnce(),
                    "Revert must restore the PartitionKeyRangeLocation PPCB state to its pre-change value.");

                // Clear the failure hook and re-fire with the SAME latestIsEnabled. Because the first
                // attempt reverted the applied state, ppafEnablementChanged is true again — so this call
                // must NOT be short-circuited by the no-op guard and must re-apply the transition.
                client.ReconcileFailureHookForTests = null;
                client.UpdatePartitionLevelFailoverConfigWithAccountRefresh(latestIsEnabled: true, latestDisableCrossRegionalHedging: false);

                Assert.IsTrue(
                    client.ConnectionPolicy.EnablePartitionLevelFailover,
                    "A second refresh with the same value must re-apply the missed PPAF transition, proving the first call's revert prevented a permanent no-op.");
                Assert.IsTrue(
                    client.ConnectionPolicy.EnablePartitionLevelCircuitBreaker,
                    "Circuit breaker must be enabled in tandem when the PPAF transition is finally applied.");
            }
            finally
            {
                client.Dispose();
            }
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
    }
}
