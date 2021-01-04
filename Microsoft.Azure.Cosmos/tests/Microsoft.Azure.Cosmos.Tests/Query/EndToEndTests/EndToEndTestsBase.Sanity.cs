namespace Microsoft.Azure.Cosmos.Tests.Query.EndToEndTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public abstract partial class EndToEndTestsBase
    {
        [TestMethod]
        public async Task SimplestTestAsync()
        {
            List<CosmosObject> documents = GenerateRandomDocuments(numberOfDocuments: 1000);
            static async Task ImplementationAsync(IQueryableContainer container, IReadOnlyList<CosmosObject> documents)
            {
                _ = await ValidateQueryAsync(
                    container,
                    "SELECT * FROM c");
            }
            
            await this.RunQueryTestAsync(
                documents,
                ImplementationAsync);
        }
    }
}
