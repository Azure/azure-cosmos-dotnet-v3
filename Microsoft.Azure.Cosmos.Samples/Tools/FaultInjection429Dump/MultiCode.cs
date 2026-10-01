// ------------------------------------------------------------
// Multi-status-code Exception + Diagnostics E2E dump.
// Issues one cross-partition query per fault type and dumps the caller-visible
// exception/diagnostics shape. Used to compare BEFORE/AFTER the
// TryCatch.FromException stack-trace patch across every status code that
// flows through the query path.
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

    internal static class MultiCodeProgram
    {
        private const string EmulatorEndpoint = "https://127.0.0.1:8081/";
        private const string EmulatorKey      = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DatabaseName     = "FI429BenchDb";
        private const string ContainerName    = "FI429BenchContainer";

        // Server-error types worth exercising. We skip Gone/PartitionIsSplitting/PartitionIsMigrating because
        // those drive cache-refresh / split-retry paths that won't surface to the caller in a single attempt.
        private static readonly (string Label, FaultInjectionServerErrorType Type)[] Cases = new[]
        {
            ("429 TooManyRequests       ", FaultInjectionServerErrorType.TooManyRequests),
            ("503 ServiceUnavailable    ", FaultInjectionServerErrorType.ServiceUnavailable),
            ("500 InternalServerError   ", FaultInjectionServerErrorType.InternalServerError),
            ("408 Timeout               ", FaultInjectionServerErrorType.Timeout),
            ("449 RetryWith             ", FaultInjectionServerErrorType.RetryWith),
            ("404/1002 ReadSessionNotAvailable", FaultInjectionServerErrorType.ReadSessionNotAvailable),
            ("401 Unauthorized          ", FaultInjectionServerErrorType.Unauthorized),
        };

        public static async Task<int> RunAsync(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
            string sdkVersion = typeof(CosmosClient).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"=== Cosmos SDK assembly version : {sdkVersion} ===");
            Console.WriteLine();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>();
            Dictionary<FaultInjectionServerErrorType, FaultInjectionRule> ruleByType = new();
            foreach (var (label, type) in Cases)
            {
                FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                    id: $"q-{type}",
                    condition: new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.QueryItem)
                        .Build(),
                    result: FaultInjectionResultBuilder.GetResultBuilder(type).WithInjectionRate(1.0).Build())
                    .Build();
                rule.Disable();
                rules.Add(rule);
                ruleByType[type] = rule;
            }

            FaultInjector injector = new FaultInjector(rules);

            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode    = ConnectionMode.Direct,
                ConsistencyLevel  = ConsistencyLevel.Eventual,
                RequestTimeout    = TimeSpan.FromSeconds(2),
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
            try { await container.UpsertItemAsync(new { id = "dump1", pk = "pk-0000", n = 1 }, new PartitionKey("pk-0000")); }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Conflict) { }

            // Prime the cache with a successful query so subsequent failures are not init-time failures
            using (FeedIterator<dynamic> warm = container.GetItemQueryIterator<dynamic>("SELECT c.n FROM c"))
            {
                while (warm.HasMoreResults) { _ = await warm.ReadNextAsync(); }
            }
            Console.WriteLine("[primed routing/cache]");
            Console.WriteLine();

            foreach (var (label, type) in Cases)
            {
                FaultInjectionRule rule = ruleByType[type];
                rule.Enable();
                Exception caught = null;
                try
                {
                    using FeedIterator<dynamic> it = container.GetItemQueryIterator<dynamic>(
                        "SELECT c.n, c.pk FROM c ORDER BY c.n",
                        requestOptions: new QueryRequestOptions { MaxConcurrency = -1, MaxItemCount = 100 });
                    while (it.HasMoreResults)
                    {
                        FeedResponse<dynamic> _ = await it.ReadNextAsync();
                    }
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                rule.Disable();

                Dump(label, caught);
            }
            return 0;
        }

        private static void Dump(string label, Exception ex)
        {
            Console.WriteLine(new string('=', 100));
            Console.WriteLine($"CASE: {label}");
            Console.WriteLine(new string('=', 100));
            if (ex == null)
            {
                Console.WriteLine("(no exception caught — fault did not propagate to caller)");
                Console.WriteLine();
                return;
            }
            Console.WriteLine($"  Chain.depth = {ChainDepth(ex)}");
            Console.WriteLine($"  Outer.Type  = {ex.GetType().FullName}");

            CosmosException ce = ex as CosmosException ?? FindInner<CosmosException>(ex);
            if (ce != null)
            {
                Console.WriteLine($"  Status      = {(int)ce.StatusCode} ({ce.StatusCode})  SubStatus={ce.SubStatusCode}  RetryAfter={ce.RetryAfter}");
                Console.WriteLine($"  ActivityId  = {ce.ActivityId}");
                Console.WriteLine($"  Message     = {Truncate(ce.Message, 200)}");
#pragma warning disable CDX1002
                string stack = ce.StackTrace ?? string.Empty;
                Console.WriteLine($"  StackBytes  = {stack.Length}");
                string firstFrame = stack.Split('\n')[0].TrimEnd();
                Console.WriteLine($"  StackTop    = {Truncate(firstFrame, 140)}");
#pragma warning restore CDX1002
                string diag = ce.Diagnostics?.ToString() ?? "(null)";
                int directCallsIdx = diag.IndexOf("\"DirectCalls\":", StringComparison.Ordinal);
                string directCallsSnippet = directCallsIdx >= 0 ? diag.Substring(directCallsIdx, Math.Min(120, diag.Length - directCallsIdx)) : "(no DirectCalls summary)";
                Console.WriteLine($"  Diag.summary= {directCallsSnippet}");
                Console.WriteLine($"  Diag.length = {diag.Length} bytes");
            }
            else
            {
                Console.WriteLine($"  (no CosmosException in chain)");
                Console.WriteLine($"  Message     = {Truncate(ex.Message, 200)}");
            }
            Console.WriteLine();
        }

        private static int ChainDepth(Exception e)
        {
            int d = 0; while (e != null) { d++; e = e.InnerException; } return d;
        }

        private static T FindInner<T>(Exception e) where T : Exception
        {
            while (e != null) { if (e is T t) return t; e = e.InnerException; } return null;
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            return s.Length <= n ? s : s.Substring(0, n) + "...";
        }
    }
}
