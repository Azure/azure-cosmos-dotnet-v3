namespace Microsoft.Azure.Cosmos.Tests.Aot
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests.Aot.Common;

    [TestClass]
    public class RestHelperTest
    {
        [TestMethod]
        public async Task TestRestHelperMethods()
        {
            (string accountEndpoint, string cosmosKey) = TestUtil.GetAccountDetails();

            if (string.IsNullOrEmpty(cosmosKey) || string.IsNullOrEmpty(accountEndpoint))
            {
                Console.WriteLine("Missing one or more configuration values. Please make sure to set them in the `environmenVariables` section");
                return;
            }
            
            string databaseId = "testdb";
            string containerId = "c1";
            RestHelper.Item item1 = new RestHelper.Item("id1", "pk1", "value1");
            RestHelper.Item item11 = new RestHelper.Item("id11", "pk1", "value-11");
            RestHelper.Item item2 = new RestHelper.Item("id2", "pk1", "value2");
            RestHelper.Item item3 = new RestHelper.Item("id3", "pk2", "value3");

            RestHelper restHelper = new RestHelper(accountEndpoint, cosmosKey, outputToConsole: true);

            await restHelper.CreateDatabase(databaseId, RestHelper.DatabaseThoughputMode.@fixed);

            await restHelper.ListDatabases();
            await restHelper.GetDatabase(databaseId);

            await restHelper.CreateContainer(databaseId, containerId, RestHelper.DatabaseThoughputMode.none);

            await restHelper.GetContainer(databaseId, containerId);
            await restHelper.GetContainerPartitionKeys(databaseId, containerId);

            await restHelper.CreateStoredProcedure(databaseId, containerId, "sproc1");
            await restHelper.ExecuteStoredProcedure(databaseId, containerId, "sproc1");
            await restHelper.DeleteStoredProcedure(databaseId, containerId, "sproc1");

            await restHelper.CreateDocument(databaseId, containerId, item1);
            await restHelper.CreateDocument(databaseId, containerId, item2);
            await restHelper.CreateDocument(databaseId, containerId, item3);

            await restHelper.PatchDocument(databaseId, containerId, id: item1.id, partitionKey: item1.pk);
            await restHelper.ReplaceDocument(databaseId, containerId, id: item1.id, newItem: item11);

            await restHelper.ListDocuments(databaseId, containerId, partitionKey: item1.pk);
            await restHelper.GetDocument(databaseId, containerId, id: item2.id, partitionKey: item2.pk);

            await restHelper.QueryDocuments(databaseId, containerId, partitionKey: item1.pk);
            await restHelper.QueryDocumentsCrossPartition(databaseId, containerId);

            await restHelper.DeleteDocument(databaseId, containerId, id: item11.id, partitionKey: item11.pk);
            await restHelper.DeleteDocument(databaseId, containerId, id: item2.id, partitionKey: item2.pk);
            await restHelper.DeleteDocument(databaseId, containerId, id: item3.id, partitionKey: item3.pk);

            await restHelper.DeleteContainer(databaseId, containerId);

            await restHelper.DeleteDatabase(databaseId);
        }
    }
}
