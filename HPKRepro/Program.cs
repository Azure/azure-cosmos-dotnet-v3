using System.Diagnostics;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace HPKSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                MainAsync(args).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: {0}", ex);
            }

            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }

        static async Task MainAsync(string[] args)
        {
            if (args.Length == 0 || String.IsNullOrWhiteSpace(args[0]))
            {
                throw new ArgumentException("First command line argument needs to be the read-only connection string.", "args[0]");
            }

            using CosmosClient client = new CosmosClient(args[0]);
            Container container = client.GetContainer("Leaderboard", "Data");
            //await Correct_ButUnintuitive_QueryResultsAsync(container);

            // BUG BUG BUG
            // Using QueryRequestOptions.PartitionKey just hangs when a HPK spans more than one Physical partition
            await Hang_FilteringByRequestOptionsWithPKAsync(container);

            // BUG BUG BUG
            // Using FeedRange of partial HPK to filter query results does not work
            // await FunctionalBug_NotFilteringByEpkAsync(container);
        }

        static async Task Correct_ButUnintuitive_QueryResultsAsync(Container container)
        {
            Console.WriteLine();
            Console.WriteLine("Correct_ButUnintuitive_QueryResultsAsync");
            Console.WriteLine("-------------------------------------------");

            var shardId = "0";
            var leaderboardId = "095bceb0-af08-432b-adaf-3f81bfaf01ec:0:::";
            //var leaderboardId = "0d09115f-12bd-4635-9889-b19bef9d7dfd:0:::";

            var queryDefinition = new QueryDefinition(
                $"SELECT * FROM c "
                + $"WHERE c.ShardId = @shardId "
                + $"AND c.LeaderboardId = @leaderboardId")
                .WithParameter("@shardId", shardId)
                .WithParameter("@leaderboardId", leaderboardId);

            var partitionKey = new PartitionKeyBuilder().Add(shardId).Add(leaderboardId).Build();

            int resultCount = await ExecuteQueryWithHPKAsync(container, queryDefinition, pk: null, feedRange: null);
            if (resultCount != 10000)
            {
                throw new InvalidDataException($"ResultCount is {resultCount} instead of 10000");
            }
        }

        static async Task Hang_FilteringByRequestOptionsWithPKAsync(Container container)
        {
            Console.WriteLine();
            Console.WriteLine("Hang_FilteringByRequestOptionsWithPKAsync");
            Console.WriteLine("-------------------------------------------");


            var shardId = "0";
            var leaderboardId = "095bceb0-af08-432b-adaf-3f81bfaf01ec:0:::";
            //var leaderboardId = "0d09115f-12bd-4635-9889-b19bef9d7dfd:0:::";

            var queryDefinition = new QueryDefinition(
                $"SELECT * FROM c ");

            var partitionKey = new PartitionKeyBuilder().Add(shardId).Add(leaderboardId).Build();

            int resultCount = await ExecuteQueryWithHPKAsync(container, queryDefinition, pk: partitionKey, feedRange: null);
            if (resultCount != 10000)
            {
                throw new InvalidDataException($"ResultCount is {resultCount} instead of 10000");
            }
        }

        static async Task FunctionalBug_NotFilteringByEpkAsync(Container container)
        {
            Console.WriteLine();
            Console.WriteLine("FunctionalBug_NotFilteringByEpkAsync");
            Console.WriteLine("-------------------------------------------");

            var shardId = "0";
            var leaderboardId = "095bceb0-af08-432b-adaf-3f81bfaf01ec:0:::";
            //var leaderboardId = "0d09115f-12bd-4635-9889-b19bef9d7dfd:0:::";

            var queryDefinition = new QueryDefinition(
                $"SELECT * FROM c ");

            var partitionKey = new PartitionKeyBuilder().Add(shardId).Add(leaderboardId).Build();

            int resultCount = await ExecuteQueryWithHPKAsync(container, queryDefinition, pk: null, feedRange: FeedRange.FromPartitionKey(partitionKey));
            if (resultCount != 10000)
            {
                throw new InvalidDataException($"ResultCount is {resultCount} instead of 10000");
            }
        }

        static async Task<int> ExecuteQueryWithHPKAsync(
            Container container,
            QueryDefinition queryDefinition,
            PartitionKey? pk,
            FeedRange? feedRange)
        {
            var requestOptions = new QueryRequestOptions();
            if (pk != null)
            {
                requestOptions.PartitionKey = pk;
            }

            List<JObject> results = new List<JObject>();
            using var iterator = container.GetItemQueryIterator<JObject>(feedRange, queryDefinition, continuationToken: null, requestOptions: requestOptions);
            int i = 1;
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();

                Console.WriteLine($"Page {i}: PKRangeId : {response.Headers["x-ms-cosmos-physical-partition-id"]}");
                Console.WriteLine($"Page {i}: ActivityId : {response.ActivityId}");
                Console.WriteLine($"Page {i}: Total documents for Partition Key : {response.Count()}");
                Console.WriteLine($"Page {i}: Request charge: {response.RequestCharge}");
                results.AddRange(response.Resource);
                i++;
            }

            return results.Count;
        }
    }
}
