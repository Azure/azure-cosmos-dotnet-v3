namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientConfigurationDiagnosticTest : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ClientConfigTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response.Diagnostics);
            ITrace trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            Assert.AreEqual(trace.Data.Count, 1);
            ClientConfigurationTraceDatum clientConfigurationTraceDatum = (ClientConfigurationTraceDatum)trace.Data["Client Configuration"];
            Assert.IsNotNull(clientConfigurationTraceDatum.UserAgentContainer.UserAgent);
        }

        [TestMethod]
        public void CleintConfigWithOptionsTest()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(50),
                OpenTcpConnectionTimeout = TimeSpan.FromSeconds(30),
                GatewayModeMaxConnectionLimit = 20,
                MaxRequestsPerTcpConnection = 30,
                MaxTcpConnectionsPerEndpoint = 30,
                LimitToEndpoint = true,
                ConsistencyLevel = ConsistencyLevel.Session
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(options);
            RntbdConnectionConfig tcpconfig = cosmosClient.ClientConfigurationTraceDatum.RntbdConnectionConfig;
            Assert.AreEqual(tcpconfig.ConnectionTimeout, 30);
            Assert.AreEqual(tcpconfig.IdleConnectionTimeout, -1);
            Assert.AreEqual(tcpconfig.MaxRequestsPerChannel, 30);
            Assert.AreEqual(tcpconfig.TcpEndpointRediscovery, false);

            GatewayConnectionConfig gwConfig = cosmosClient.ClientConfigurationTraceDatum.GatewayConnectionConfig;
            Assert.AreEqual(gwConfig.UserRequestTimeout, 50);
            Assert.AreEqual(gwConfig.MaxConnectionLimit, 20);

            ConsistencyConfig consistencyConfig = cosmosClient.ClientConfigurationTraceDatum.ConsistencyConfig;
            Assert.AreEqual(consistencyConfig.ConsistencyLevel.Value, ConsistencyLevel.Session);
        }

        [TestMethod]
        public void ConsistencyConfigSerializationTest()
        {
            List<string> preferredRegions = new List<string> { "EastUS", "WestUs" };
            ConsistencyLevel consistencyLevel = ConsistencyLevel.Session;

            ConsistencyConfig consistencyConfig = new ConsistencyConfig(consistencyLevel, preferredRegions);
            Assert.AreEqual(consistencyConfig.ToString(), "(consistency: Session, prgns:[EastUS, WestUs])");

            ConsistencyConfig consistencyConfigWithNull = new ConsistencyConfig(consistencyLevel: null,
                                                                                preferredRegions: null);

            Assert.AreEqual(consistencyConfigWithNull.ToString(), "(consistency: NotSet, prgns:[])");
        }

        [TestMethod]
        public async Task CachedSerializationTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            ITrace trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            ClientConfigurationTraceDatum clientConfigurationTraceDatum = (ClientConfigurationTraceDatum)trace.Data["Client Configuration"];
            Assert.IsNotNull(clientConfigurationTraceDatum.SerializedJson);
            TestCommon.CreateCosmosClient();
            response = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            clientConfigurationTraceDatum = (ClientConfigurationTraceDatum)trace.Data["Client Configuration"];
            Assert.IsNotNull(clientConfigurationTraceDatum.SerializedJson);
        }
    }
}
