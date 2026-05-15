namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;
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
