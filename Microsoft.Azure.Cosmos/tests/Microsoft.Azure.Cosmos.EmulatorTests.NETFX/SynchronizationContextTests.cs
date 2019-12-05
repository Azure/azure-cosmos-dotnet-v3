//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SynchronizationContextTests : BaseCosmosClientHelper
    {
        private Container Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            await base.TestCleanup();
        }

        [TestMethod]
        [Timeout(30000)]
        public void VerifySynchronizationContextDoesNotLock()
        {
            // Using Windows Form context to block similarly than ASP.NET NETFX would
            WindowsFormsSynchronizationContext synchronizationContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            this.database.ReadStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            this.database.ReadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = this.Container.CreateItemAsync<ToDoActivity>(item: testItem).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.IsNotNull(response);

            this.Container.GetItemLinqQueryable<ToDoActivity>(
                allowSynchronousQueryExecution: true,
                requestOptions: new QueryRequestOptions()
                {
                }).ToList();

            ItemResponse<ToDoActivity> deleteResponse = this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.IsNotNull(deleteResponse);
        }
    }
}
