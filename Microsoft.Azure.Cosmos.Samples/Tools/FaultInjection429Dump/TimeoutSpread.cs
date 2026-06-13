// Multi-run 408 timeout case to show run-to-run noise in DirectCalls count.
namespace Microsoft.Azure.Cosmos.Samples.FaultInjection429Dump
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection;

    internal static class TimeoutSpreadProgram
    {
        private const string EmulatorEndpoint = "https://127.0.0.1:8081/";
        private const string EmulatorKey      = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DatabaseName     = "FI429BenchDb";
        private const string ContainerName    = "FI429BenchContainer";
        private const int    Runs             = 5;

        public static async Task<int> RunAsync(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
            string sdkVersion = typeof(CosmosClient).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"=== Cosmos SDK assembly version : {sdkVersion} ===");
            Console.WriteLine($"408 Timeout: {Runs} runs to show run-to-run spread of DirectCalls and wall time.");
            Console.WriteLine();

            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: "q-Timeout",
                condition: new FaultInjectionConditionBuilder().WithOperationType(FaultInjectionOperationType.QueryItem).Build(),
                result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Timeout).WithInjectionRate(1.0).Build())
                .Build();
            rule.Disable();

            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { rule });
            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Eventual,
                RequestTimeout = TimeSpan.FromSeconds(2),
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(2),
                MaxRetryAttemptsOnRateLimitedRequests = 1,
                LimitToEndpoint = true,
                HttpClientFactory = () =>
                {
                    HttpClientHandler handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                    };
                    return new HttpClient(handler);
                },
            };

            using CosmosClient client = new CosmosClient(EmulatorEndpoint, EmulatorKey, injector.GetFaultInjectionClientOptions(options));
            Database db = (await client.CreateDatabaseIfNotExistsAsync(DatabaseName)).Database;
            Container container = (await db.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerName, "/pk"), throughput: 10000)).Container;
            try { await container.UpsertItemAsync(new { id = "dump1", pk = "pk-0000", n = 1 }, new PartitionKey("pk-0000")); }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Conflict) { }
            using (FeedIterator<dynamic> warm = container.GetItemQueryIterator<dynamic>("SELECT c.n FROM c"))
            {
                while (warm.HasMoreResults) { _ = await warm.ReadNextAsync(); }
            }

            Console.WriteLine($"{"Run",4} {"WallMs",8} {"DirectCalls",14} {"GatewayCalls",14} {"DiagBytes",10} {"FinalStatus",18}");
            Regex callsRe = new Regex("\"\\((?<status>\\d+), (?<sub>\\d+)\\)\":(?<count>\\d+)", RegexOptions.Compiled);

            int[] directs = new int[Runs];
            long[] walls = new long[Runs];

            for (int r = 0; r < Runs; r++)
            {
                rule.Enable();
                Stopwatch sw = Stopwatch.StartNew();
                CosmosException caught = null;
                try
                {
                    using FeedIterator<dynamic> it = container.GetItemQueryIterator<dynamic>(
                        "SELECT c.n, c.pk FROM c ORDER BY c.n",
                        requestOptions: new QueryRequestOptions { MaxConcurrency = -1, MaxItemCount = 100 });
                    while (it.HasMoreResults) { _ = await it.ReadNextAsync(); }
                }
                catch (CosmosException ce) { caught = ce; }
                sw.Stop();
                rule.Disable();

                string diag = caught?.Diagnostics?.ToString() ?? string.Empty;
                int direct = 0, gw = 0;
                int dcIdx = diag.IndexOf("\"DirectCalls\":", StringComparison.Ordinal);
                int gwIdx = diag.IndexOf("\"GatewayCalls\":", StringComparison.Ordinal);
                if (dcIdx >= 0)
                {
                    int close = diag.IndexOf('}', dcIdx);
                    foreach (Match m in callsRe.Matches(diag.Substring(dcIdx, close - dcIdx)))
                    {
                        direct += int.Parse(m.Groups["count"].Value);
                    }
                }
                if (gwIdx >= 0)
                {
                    int close = diag.IndexOf('}', gwIdx);
                    foreach (Match m in callsRe.Matches(diag.Substring(gwIdx, close - gwIdx)))
                    {
                        gw += int.Parse(m.Groups["count"].Value);
                    }
                }
                directs[r] = direct;
                walls[r] = sw.ElapsedMilliseconds;
                string finalStatus = caught != null ? $"{(int)caught.StatusCode}/{caught.SubStatusCode}" : "(none)";
                Console.WriteLine($"{r,4} {sw.ElapsedMilliseconds,8} {direct,14} {gw,14} {diag.Length,10} {finalStatus,18}");
            }

            Console.WriteLine();
            Console.WriteLine($"DirectCalls min={directs.Min()} max={directs.Max()} mean={directs.Average():F1} spread={directs.Max() - directs.Min()}");
            Console.WriteLine($"Wall(ms)    min={walls.Min()} max={walls.Max()} mean={walls.Average():F0}");
            return 0;
        }
    }
}
