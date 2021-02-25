namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
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
            Assert.AreEqual(clientConfigurationTraceDatum.NumberOfClients, 1);
        }
    }
}
