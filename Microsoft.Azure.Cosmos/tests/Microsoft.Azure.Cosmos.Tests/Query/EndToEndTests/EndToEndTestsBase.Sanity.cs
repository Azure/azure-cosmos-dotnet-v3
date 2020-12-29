namespace Microsoft.Azure.Cosmos.Tests.Query.EndToEndTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public abstract partial class EndToEndTestsBase
    {
        [TestMethod]
        public async Task SimplestTest()
        {
            List<CosmosObject> documents = GenerateRandomDocuments(numberOfDocuments: 1000);
            static async Task ImplementationAsync(IQueryableContainer container, IReadOnlyList<CosmosObject> documents)
            {
                List<CosmosElement> queryResults = await RunQueryAsync(
                    container,
                    "SELECT * FROM c");

                Assert.AreEqual(
                    documents.Count,
                    queryResults.Count);
            }
            
            await this.RunQueryTestAsync(
                documents,
                ImplementationAsync);
        }
    }
}
