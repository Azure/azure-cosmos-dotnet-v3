// ------------------------------------------------------------
// Exception + Diagnostics E2E dump for a single FaultInjection-induced 429
// on a cross-partition query. Used to compare SDK behavior before/after
// the TryCatch.FromException stack-trace patch.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Samples.FaultInjection429Dump
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection;

    internal static class Program
    {
        private const string EmulatorEndpoint = "https://127.0.0.1:8081/";
        private const string EmulatorKey      = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DatabaseName     = "FI429BenchDb";
        private const string ContainerName    = "FI429BenchContainer";

        private static async Task<int> Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "multi")
            {
                return await MultiCodeProgram.RunAsync(args);
            }
            if (args.Length > 0 && args[0] == "timeout-spread")
            {
                return await TimeoutSpreadProgram.RunAsync(args);
            }
            return await SingleRunAsync();
        }

        private static async Task<int> SingleRunAsync()
        {
            ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
            string sdkVersion = typeof(CosmosClient).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"=== Cosmos SDK assembly version : {sdkVersion} ===");
            Console.WriteLine();

            FaultInjectionRule queryRule = new FaultInjectionRuleBuilder(
                    id: "dump-query-429",
                    condition: new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.QueryItem)
                        .Build(),
                    result: FaultInjectionResultBuilder
                        .GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithInjectionRate(1.0)
                        .Build())
                .Build();
            queryRule.Disable();

            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { queryRule });

            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode    = ConnectionMode.Direct,
                ConsistencyLevel  = ConsistencyLevel.Eventual,
                RequestTimeout    = TimeSpan.FromSeconds(1),
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(2),
                MaxRetryAttemptsOnRateLimitedRequests = 1,
                LimitToEndpoint   = true,
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

            try
            {
                await container.UpsertItemAsync(new { id = "dump1", pk = "pk-0000", n = 1 }, new PartitionKey("pk-0000"));
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Conflict) { }

            using (FeedIterator<dynamic> warm = container.GetItemQueryIterator<dynamic>("SELECT c.n, c.pk FROM c ORDER BY c.n"))
            {
                while (warm.HasMoreResults) { _ = await warm.ReadNextAsync(); }
            }
            Console.WriteLine("[primed routing/cache]");

            queryRule.Enable();
            Console.WriteLine("[rule enabled - issuing query that will see 429]");
            try
            {
                using FeedIterator<dynamic> it = container.GetItemQueryIterator<dynamic>(
                    "SELECT c.n, c.pk FROM c ORDER BY c.n",
                    requestOptions: new QueryRequestOptions { MaxConcurrency = -1, MaxItemCount = 100 });
                while (it.HasMoreResults)
                {
                    FeedResponse<dynamic> page = await it.ReadNextAsync();
                    Console.WriteLine($"[unexpected success page Count={page.Count}]");
                }
            }
            catch (Exception caught)
            {
                DumpException(caught);
            }
            queryRule.Disable();
            return 0;
        }

        private static void DumpException(Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 88));
            Console.WriteLine("CAUGHT EXCEPTION (caller-visible) - what an SDK consumer sees");
            Console.WriteLine(new string('=', 88));

            int depth = 0;
            Exception cur = ex;
            while (cur != null)
            {
                Console.WriteLine($"--- chain[{depth}] ---");
                Console.WriteLine($"  Type     : {cur.GetType().FullName}");
                Console.WriteLine($"  Message  : {Truncate(cur.Message, 220)}");
                if (cur is CosmosException ce)
                {
                    Console.WriteLine($"  Status   : {(int)ce.StatusCode} ({ce.StatusCode})  SubStatus={ce.SubStatusCode}");
                    Console.WriteLine($"  ActivityId={ce.ActivityId}  RetryAfter={ce.RetryAfter}");
                }
#pragma warning disable CDX1002
                Console.WriteLine($"  HasStack : {(cur.StackTrace != null ? "yes" : "no")} (length={cur.StackTrace?.Length ?? 0})");
                if (cur.StackTrace != null)
                {
                    Console.WriteLine("  StackTrace (first 6 frames):");
                    int n = 0;
                    foreach (string line in cur.StackTrace.Split('\n'))
                    {
                        if (++n > 6) break;
                        Console.WriteLine("    " + line.TrimEnd());
                    }
                }
#pragma warning restore CDX1002
                cur = cur.InnerException;
                depth++;
            }
            Console.WriteLine();
            Console.WriteLine("--- ex.ToString() (first 1500 chars) ---");
#pragma warning disable CDX1003
            Console.WriteLine(Truncate(ex.ToString(), 1500));
#pragma warning restore CDX1003

            if (ex is CosmosException cosmosEx)
            {
                Console.WriteLine();
                Console.WriteLine(new string('-', 88));
                Console.WriteLine("DIAGNOSTICS (CosmosException.Diagnostics.ToString(), first 1800 chars)");
                Console.WriteLine(new string('-', 88));
                string diag = cosmosEx.Diagnostics?.ToString() ?? "(null)";
                Console.WriteLine(Truncate(diag, 1800));
            }
            Console.WriteLine();
            Console.WriteLine(new string('=', 88));
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            return s.Length <= n ? s : s.Substring(0, n) + "...[truncated]";
        }
    }
}
