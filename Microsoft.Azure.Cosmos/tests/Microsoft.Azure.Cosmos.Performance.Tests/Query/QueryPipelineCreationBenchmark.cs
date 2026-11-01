// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;

    /// <summary>
    /// Runs a query scoped to a partition key against the mocked transport, with query plan retrieval bypassed
    /// (AZURE_COSMOS_BYPASS_QUERY_PARSING, or no ServiceInterop on non-Windows and 32-bit hosts). Creating the query
    /// pipeline then parses the query with SqlQueryParser to decide whether it can run as a passthrough query,
    /// which happens for every query execution.
    /// </summary>
    [MemoryDiagnoser]
    public class QueryPipelineCreationBenchmark
    {
        private MockedItemBenchmarkHelper helper;
        private string longQuery;

        public enum QueryLength
        {
            Short,
            Medium,
            Long,
        }

        [Params(QueryLength.Short, QueryLength.Medium, QueryLength.Long)]
        public QueryLength Length { get; set; }

        [GlobalSetup]
        public async Task GlobalSetupAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, bool.TrueString);
            this.helper = new MockedItemBenchmarkHelper();

            StringBuilder longQueryText = new StringBuilder("SELECT * FROM c WHERE c.number IN (");
            for (int i = 0; i < 1024; i++)
            {
                if (i != 0)
                {
                    longQueryText.Append(',');
                }

                longQueryText.Append(i);
            }

            this.longQuery = longQueryText.Append(')').ToString();

            // The benchmark is only meaningful if the query is parsed on the client.
            using FeedIterator iterator = this.CreateIterator();
            using ResponseMessage response = await iterator.ReadNextAsync();
            if (response.StatusCode != HttpStatusCode.OK || !response.Diagnostics.ToString().Contains("Parse Query"))
            {
                throw new InvalidOperationException("The query was not parsed while creating the query pipeline.");
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.helper.TestClient.Dispose();
            Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, null);
        }

        [Benchmark]
        public async Task QuerySinglePartition()
        {
            using FeedIterator iterator = this.CreateIterator();
            using ResponseMessage response = await iterator.ReadNextAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Failed with status code {response.StatusCode}");
            }
        }

        private FeedIterator CreateIterator()
        {
            string query = this.Length switch
            {
                QueryLength.Short => "SELECT * FROM c",
                QueryLength.Medium => "SELECT * FROM c WHERE c.name = 'John' AND c.age = 35 OFFSET 5 LIMIT 10",
                QueryLength.Long => this.longQuery,
                _ => throw new ArgumentOutOfRangeException(nameof(this.Length)),
            };

            return this.helper.TestContainer.GetItemQueryStreamIterator(
                query,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("dummyValue"),
                    MaxItemCount = 1000,
                });
        }
    }
}
