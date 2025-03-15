namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestIssueRepro
    {
        [TestMethod]
        public async Task Repro()
        {
            CosmosClient client = SDK.EmulatorTests.TestCommon.CreateCosmosClient(false);

            string query = @"SELECT DISTINCT VALUE v2 
                FROM root 
                JOIN (
                    SELECT DISTINCT VALUE v0 
                    FROM root 
                    JOIN v0 IN root[""Parents""]) AS v2 
                    WHERE (LENGTH(v2[""FamilyName""]) > 10) 
                    ORDER BY v2 ASC";

            foreach (bool ode in new[] { true,
                                         false
                                         })
            {
                QueryRequestOptions queryRequestOptions = new QueryRequestOptions { EnableOptimisticDirectExecution = ode };
                FeedIterator<object> iterator = client.GetDatabaseQueryIterator<object>(query, requestOptions: queryRequestOptions);

                List<object> result = new ();
                try
                {
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<object> response = await iterator.ReadNextAsync();
                        result.AddRange(response.Resource);
                    }
                    Assert.Fail("Should receive exception");
                }
                catch(CosmosException ex)
                {
                    Assert.IsTrue(ex.Message.Contains(@"Reason: (Message: {""Errors"":[""'ORDER BY' is not supported for the target resource type.""]}"));
                }
            }
        }

        [TestMethod]
        public async Task ReproLinq()
        {
            CosmosClient client = SDK.EmulatorTests.TestCommon.CreateCosmosClient(false);
            DatabaseResponse dbResponse = await client.CreateDatabaseIfNotExistsAsync("db1");
            Database db = client.GetDatabase("db1");
            ContainerResponse containerResponse = await db.CreateContainerIfNotExistsAsync(
                new ContainerProperties()
                {
                    Id = "c1",
                    PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { "/pk" } }
                });
            Container container = db.GetContainer("c1");

            //string query = @"SELECT DISTINCT VALUE v2 
            //    FROM root 
            //    JOIN (
            //        SELECT DISTINCT VALUE v0 
            //        FROM root 
            //        JOIN v0 IN root[""Parents""]) AS v2 
            //        WHERE (LENGTH(v2[""FamilyName""]) > 10) 
            //        ORDER BY v2 ASC";

            foreach (bool ode in new[] { true,
                                         false
                                         })
            {
                QueryRequestOptions queryRequestOptions = new QueryRequestOptions { EnableOptimisticDirectExecution = ode };
                IOrderedQueryable<Family> queryable = container.GetItemLinqQueryable<Family>(allowSynchronousQueryExecution: true, requestOptions: queryRequestOptions);
                IQueryable<Parent> parentsQueryable = queryable.SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f);
                try
                {
                    List<Parent> parents = parentsQueryable.ToList();
                    Assert.Fail($"ODE = {ode} - expected to receive exception. Instead query succeeded, returned '{parents.Count}' results.");
                }
                catch (CosmosException ex)
                {
                    Assert.IsTrue(ex.Message.Contains(@"Reason: (Message: {""Errors"":[""Order-by over correlated collections is not supported.""]}"));
                }
            }
        }
    }
}
