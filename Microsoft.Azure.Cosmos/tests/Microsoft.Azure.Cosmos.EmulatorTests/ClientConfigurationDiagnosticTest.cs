namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
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
        public void ClientConfigWithOptionsTest()
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
            Assert.AreEqual(tcpconfig.TcpEndpointRediscovery, true);

            GatewayConnectionConfig gwConfig = cosmosClient.ClientConfigurationTraceDatum.GatewayConnectionConfig;
            Assert.AreEqual(gwConfig.UserRequestTimeout, 50);
            Assert.AreEqual(gwConfig.MaxConnectionLimit, 20);

            ConsistencyConfig consistencyConfig = cosmosClient.ClientConfigurationTraceDatum.ConsistencyConfig;
            Assert.AreEqual(consistencyConfig.ConsistencyLevel.Value, ConsistencyLevel.Session);

            CosmosClientOptions clientOptions = new CosmosClientOptions 
            {
                ApplicationRegion = "East US"
            };

            CosmosClientContext context = ClientContextCore.Create(
                cosmosClient,
                clientOptions);

            ClientConfigurationTraceDatum clientConfig = new ClientConfigurationTraceDatum(context, DateTime.UtcNow);
            Assert.AreEqual(clientConfig.ConsistencyConfig.ApplicationRegion, "East US");
            Assert.IsNull(clientConfig.ConsistencyConfig.PreferredRegions);

            Assert.AreEqual(clientConfig.ConnectionMode, ConnectionMode.Direct);
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
            context = ClientContextCore.Create(
                cosmosClient,
                clientOptions);
            clientConfig = new ClientConfigurationTraceDatum(context, DateTime.UtcNow);
            Assert.AreEqual(clientConfig.ConnectionMode, ConnectionMode.Gateway);
        }

        [TestMethod]
        public void ConsistencyConfigSerializationTest()
        {
            List<string> preferredRegions = new List<string> { "EastUS", "WestUs" };
            ConsistencyLevel consistencyLevel = ConsistencyLevel.Session;
            string appRegion = "EastUS";

            ConsistencyConfig consistencyConfig = new ConsistencyConfig(consistencyLevel, preferredRegions, appRegion);
            Assert.AreEqual(consistencyConfig.ToString(), "(consistency: Session, prgns:[EastUS, WestUs], apprgn: EastUS)");

            ConsistencyConfig consistencyConfigWithNull = new ConsistencyConfig(consistencyLevel: null,
                                                                                preferredRegions: null,
                                                                                applicationRegion: null);

            Assert.AreEqual(consistencyConfigWithNull.ToString(), "(consistency: NotSet, prgns:[], apprgn: )");
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
            Assert.AreEqual(clientConfigurationTraceDatum.ProcessorCount, Environment.ProcessorCount);
            string deserializedJson = Encoding.UTF8.GetString(clientConfigurationTraceDatum.SerializedJson.Span);
            Assert.IsTrue(deserializedJson.Contains("ConnectionMode"));
        }
    }
}
