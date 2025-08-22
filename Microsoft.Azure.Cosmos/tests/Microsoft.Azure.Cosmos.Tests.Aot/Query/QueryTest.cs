namespace Microsoft.Azure.Cosmos.Tests.Aot.Query
{
    using Microsoft.Azure.Cosmos.Tests.Aot.Common;

    [TestClass]
    public class QueryTest
    {
        private const string DatabaseName = "QueryTest";
        private const string CollectionName = "c1";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await TestUtil.SetupTestDataAsync(DatabaseName, CollectionName);
        }

        [TestMethod]
        public async Task SelectAll()
        {
            string query = "SELECT * FROM c";
            
            FeedIterator<object> iterator = TestUtil.GetQueryIterator<object>(DatabaseName, CollectionName, query);
            List<object> results = new List<object>();

            while (iterator.HasMoreResults)
            {
                FeedResponse<object> response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            Assert.AreEqual(100, results.Count);
            foreach (object item in results)
            {
                Console.WriteLine(item);
            }
        }
    }
}
