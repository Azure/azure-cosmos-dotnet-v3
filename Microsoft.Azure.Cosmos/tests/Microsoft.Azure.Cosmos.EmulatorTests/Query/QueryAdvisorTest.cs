//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;

    [TestClass]
    [TestCategory("Query")]
    public sealed class QueryAdvisorTest : QueryTestsBase
    {
        [TestMethod]
        [Ignore] // Ignoreed until the emulator supports query advisor
        public async Task TestQueryAdvisorExistence()
        {
            // Generate some documents
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync,
                "/id");

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                string query = string.Format("SELECT VALUE r.id FROM root r WHERE CONTAINS(r.name, \"Abc\") ");

                // Test using GetItemQueryIterator
                QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateQueryAdvice = true, PopulateIndexMetrics = true};

                FeedIterator<CosmosElement> itemQuery = container.GetItemQueryIterator<CosmosElement>(
                    query,
                    requestOptions: requestOptions);

                while (itemQuery.HasMoreResults)
                {
                    FeedResponse<CosmosElement> page = await itemQuery.ReadNextAsync();
                    
                    Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.QueryAdvice), "Expected query advice header for query");
                    Assert.IsNotNull(page.QueryAdvice, "Expected query advice text for query");
                }

                // Test using Stream API
                using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryText: query,
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    PopulateQueryAdvice = true,
                }))
                {
                    using (ResponseMessage response = await feedIterator.ReadNextAsync())
                    {
                        Assert.IsNotNull(response.Content);
                        Assert.IsTrue(response.Headers.AllKeys().Length > 1);
                        Assert.IsNotNull(response.Headers.QueryAdvice, "Expected query advice header for query");
                        Assert.IsNotNull(response.QueryAdvice, "Expected query advice text for query");
                        Console.WriteLine(response.QueryAdvice);
                    }
                }
            }
        }
        
    }
}
