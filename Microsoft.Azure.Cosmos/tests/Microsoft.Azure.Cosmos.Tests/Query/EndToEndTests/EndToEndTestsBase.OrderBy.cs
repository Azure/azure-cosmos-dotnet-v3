namespace Microsoft.Azure.Cosmos.Tests.Query.EndToEndTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public abstract partial class EndToEndTestsBase
    {
        [TestMethod]
        public async Task OrderByAsync()
        {
            List<CosmosObject> documents = GenerateRandomDocuments(numberOfDocuments: 100);
            static async Task ImplementationAsync(IQueryableContainer container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (string sortOrder in new string[] { string.Empty, "ASC", "DESC" })
                {
                    string query = $"SELECT * FROM c ORDER BY c._ts {sortOrder}";
                    _ = await ValidateQueryAsync(
                        container,
                        query);
                }
            }

            await this.RunQueryTestAsync(
                documents,
                ImplementationAsync);
        }
    }
}
