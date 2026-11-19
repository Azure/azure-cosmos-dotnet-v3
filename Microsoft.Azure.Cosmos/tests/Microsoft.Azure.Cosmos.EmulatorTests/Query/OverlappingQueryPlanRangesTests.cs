//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TestCommon = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon;

    [TestClass]
    [TestCategory("QueryPlanOverlapService")]
    [DoNotParallelize]
    public sealed class OverlappingQueryPlanRangesTests
    {
        private const string LowId = "11223344-5566-7788-9900-1122334455ab";
        private const string HighId = "abcdef12-3456-7890-abcd-ef1234567890";
        private static CosmosClient client;
        private static Cosmos.Database database;
        private static ContainerInternal container;
        private static QueryPlanCaptureHandler capture;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static async Task InitializeAsync(TestContext context)
        {
            string mode = Environment.GetEnvironmentVariable("COSMOS_QUERY_PLAN_OVERLAP_SERVICE_TESTS");
            if (mode != "emulator" && mode != "live")
            {
                Assert.Inconclusive("Opt in with COSMOS_QUERY_PLAN_OVERLAP_SERVICE_TESTS=emulator (or live for an explicitly authorized disposable account).");
            }

            (string configuredEndpoint, string key) = TestCommon.GetAccountInfo();
            Uri endpoint = new Uri(Environment.GetEnvironmentVariable("COSMOS_QUERY_PLAN_OVERLAP_ENDPOINT") ?? configuredEndpoint);
            Assert.IsTrue(endpoint.Scheme == Uri.UriSchemeHttps, "An HTTPS endpoint is required.");
            Assert.IsTrue(mode == "live" || endpoint.IsLoopback, "Emulator mode must not contact a non-loopback account.");
            context.WriteLine($"Actual {mode} gateway; endpoint={endpoint}; {RuntimeInformation.FrameworkDescription}");

            HttpClientHandler transport = new HttpClientHandler();
            if (mode == "emulator")
            {
                transport.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            capture = new QueryPlanCaptureHandler { InnerHandler = transport };
            client = new CosmosClient(endpoint.ToString(), key, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => new HttpClient(capture, disposeHandler: false),
            });
            bool initialized = false;
            try
            {
                database = await client.CreateDatabaseAsync("query-plan-overlap-" + Guid.NewGuid().ToString("N"));
                context.WriteLine($"Created isolated database: {database.Id}");
                ContainerResponse response = await database.CreateContainerAsync(
                    new ContainerProperties("items", new List<string> { "/_pk", "/id" }), throughput: 400);
                container = (ContainerInternal)response.Container;
                foreach (string id in new[]
                {
                    LowId, HighId, "11223344-5566-7788-9900-1122334455cd",
                    "fedcba98-7654-3210-fedc-ba9876543210",
                    "11223344-5566-7788-9900-1122334abcde", "11223344-5566-7788-9900-112233abcdef",
                })
                {
                    await container.CreateItemAsync(new { id, _pk = "tenant-1" });
                    await container.CreateItemAsync(new { id, _pk = "other-tenant" });
                }

                await container.CreateItemAsync(new { id = LowId.ToUpperInvariant(), _pk = "tenant-1", _isSoftDeleted = true });
                await container.CreateItemAsync(new { id = HighId.ToUpperInvariant(), _pk = "tenant-1", auxStoreSoftDeleteDateTime = "2026-01-01" });
                initialized = true;
            }
            finally
            {
                if (!initialized)
                {
                    await CleanupAsync();
                }
            }
        }

        [ClassCleanup]
        public static async Task CleanupAsync()
        {
            try
            {
                if (database != null)
                {
                    await database.DeleteAsync();
                    Console.WriteLine($"Deleted isolated database: {database.Id}");
                }
            }
            finally
            {
                database = null;
                client?.Dispose();
                capture?.Dispose();
                client = null;
                capture = null;
            }
        }

        [DataTestMethod]
        [DataRow("HighHigh", true)]
        [DataRow("LowLow", true)]
        [DataRow("Mixed", true)]
        [DataRow("MixedReversed", true)]
        [DataRow("Concat", true)]
        [DataRow("CaseSensitive", true)]
        [DataRow("FiveSixLetters", true)]
        [DataRow("HighHigh", false)]
        [DataRow("LowLow", false)]
        [DataRow("Mixed", false)]
        [DataRow("MixedReversed", false)]
        [DataRow("Concat", false)]
        [DataRow("CaseSensitive", false)]
        [DataRow("FiveSixLetters", false)]
        public async Task ParameterizedQueryReturnsExpectedDocumentsAsync(string name, bool gatewayPlan)
        {
            if (!gatewayPlan && (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.ProcessArchitecture != Architecture.X64))
            {
                Assert.Inconclusive("The native comparison requires Windows x64 ServiceInterop.dll.");
            }

            string first = name == "HighHigh" || name == "MixedReversed" ? HighId : LowId;
            string second = name == "HighHigh" ? "fedcba98-7654-3210-fedc-ba9876543210"
                : name == "LowLow" ? "11223344-5566-7788-9900-1122334455cd"
                : name == "MixedReversed" ? LowId : HighId;
            if (name == "FiveSixLetters")
            {
                first = "11223344-5566-7788-9900-1122334abcde";
                second = "11223344-5566-7788-9900-112233abcdef";
            }

            string id = name == "Concat" ? @"CONCAT(c[""id""], """")" : @"c[""id""]";
            QueryDefinition query = new QueryDefinition($@"SELECT * FROM c WHERE c[""_pk""] = @p0
AND NOT IS_DEFINED(c[""_isSoftDeleted""]) AND NOT IS_DEFINED(c[""auxStoreSoftDeleteDateTime""])
AND ((STRINGEQUALS({id}, @p1, @p2) AND IS_DEFINED(c[""id""]) AND c[""id""] != null)
OR (STRINGEQUALS({id}, @p3, @p4) AND IS_DEFINED(c[""id""]) AND c[""id""] != null))")
                .WithParameter("@p0", "tenant-1").WithParameter("@p1", first).WithParameter("@p2", name != "CaseSensitive")
                .WithParameter("@p3", second).WithParameter("@p4", name != "CaseSensitive");
            MockCosmosQueryClient queryClient = new MockCosmosQueryClient(container.ClientContext, container, gatewayPlan);
            ContainerInternal queryContainer = new ContainerInlineCore(
                container.ClientContext, (DatabaseCore)container.Database, container.Id, queryClient);
            List<string> ids = new List<string>();
            string diagnostics = string.Empty;
            capture.LastPlan = null;
            try
            {
                using FeedIterator<JObject> iterator = queryContainer.GetItemQueryIterator<JObject>(query, requestOptions: new QueryRequestOptions
                {
                    EnableOptimisticDirectExecution = false,
                    EnableDistributedQueryGatewayMode = false,
                    MaxItemCount = 1,
                });
                while (iterator.HasMoreResults)
                {
                    FeedResponse<JObject> page = await iterator.ReadNextAsync();
                    ids.AddRange(page.Select(document => (string)document["id"]));
                    diagnostics += page.Diagnostics;
                }

                Assert.AreEqual(gatewayPlan, queryClient.QueryPlanCalls > 0, "Verify planner provenance independently of ConnectionMode.Gateway.");
                CollectionAssert.AreEquivalent(new[] { first, second }, ids);
                StringAssert.Contains(diagnostics, gatewayPlan ? "Gateway QueryPlan" : "Service Interop Query Plan");
            }
            finally
            {
                this.TestContext.WriteLine($"Actual {(gatewayPlan ? "gateway" : "native")} plan; case={name}; plan requests={queryClient.QueryPlanCalls}");
                this.TestContext.WriteLine($"Raw gateway plan: {capture.LastPlan ?? "<none>"}");
                this.TestContext.WriteLine(diagnostics);
            }
        }

        [DataTestMethod]
        [DataRow(@"LOWER(c.id) = ""1122ab"" OR LOWER(c.id) = ""abcdef12""")]
        [DataRow(@"UPPER(c.id) = ""1122AB"" OR UPPER(c.id) = ""ABCDEF12""")]
        [DataRow(@"STRINGEQUALS(c.id, ""1122ab"", true) OR STRINGEQUALS(c.id, ""abcdef12"", true)")]
        [DataRow(@"c.id = ""exact"" OR c.id > ""m""")]
        [DataRow(@"c.id = ""exact"" OR (c.id >= ""m"" AND c.id <= ""z"")")]
        [DataRow(@"c.id = ""exact"" OR STARTSWITH(c.id, ""pre"")")]
        [DataRow(@"c.id = ""exact"" OR c.id LIKE ""abc%""")]
        [DataRow(@"c.id = ""exact"" OR CONTAINS(c.id, ""sub"")")]
        [DataRow(@"c.id = ""exact"" OR ENDSWITH(c.id, ""ab"")")]
        [DataRow(@"c.id = ""exact"" OR RegexMatch(c.id, ""^ab"")")]
        [DataRow(@"c.id = ""exact"" OR LEFT(c.id, 2) = ""ab""")]
        [DataRow(@"c.id = ""exact"" OR SUBSTRING(c.id, 0, 2) = ""ab""")]
        [DataRow("c.id = 1 OR FLOOR(c.id) = 5")]
        [DataRow("c.id = 1 OR CEILING(c.id) = 5")]
        [DataRow("c.id = 1 OR ROUND(c.id) = 5")]
        [DataRow("c.id = 1 OR TRUNC(c.id) = 5")]
        [DataRow(@"c.id = ""exact"" OR IS_NUMBER(c.id)")]
        [DataRow(@"c.id = ""2020-01-01T00:00:00Z"" OR DateTimePart(""yyyy"", c.id) = 2025")]
        public async Task ActualGatewayPredicatePlanRoutesWithoutOverlapsAsync(string predicate)
        {
            capture.LastPlan = null;
            CosmosQueryClientCore queryClient = new CosmosQueryClientCore(container.ClientContext, container);
            PartitionedQueryExecutionInfo plan = await queryClient.ExecuteQueryPlanRequestAsync(
                container.LinkUri, ResourceType.Document, OperationType.QueryPlan,
                new SqlQuerySpec($@"SELECT * FROM c WHERE c._pk = ""t1"" AND ({predicate})"),
                partitionKey: null, supportedQueryFeatures: "NonValueAggregate",
                excludeRegions: null, Guid.NewGuid(), NoOpTrace.Singleton, CancellationToken.None);
            Assert.IsNotNull(capture.LastPlan, "A real gateway QueryPlan response must have been captured.");
            this.TestContext.WriteLine($"Raw actual gateway plan: {capture.LastPlan}");
            this.TestContext.WriteLine($"Wrapped gateway ranges: {JsonConvert.SerializeObject(plan.QueryRanges)}");
            JObject rawPlan = JObject.Parse(capture.LastPlan);
            Assert.IsTrue(rawPlan["queryRanges"]?.HasValues == true, "Record raw ranges before SDK wrapping.");
            ContainerProperties properties = await container.ReadContainerAsync();
            List<PartitionKeyRange> ranges = await queryClient.GetTargetPartitionKeyRangesAsync(
                container.LinkUri, properties.ResourceId, plan.QueryRanges, false, NoOpTrace.Singleton);
            Assert.IsTrue(ranges.Count > 0);
        }

        private sealed class QueryPlanCaptureHandler : DelegatingHandler
        {
            public string LastPlan { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                if (request.Headers.TryGetValues(HttpConstants.HttpHeaders.IsQueryPlanRequest, out IEnumerable<string> values)
                    && values.Any(value => string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase)))
                {
                    // ReadAsStringAsync buffers the body so SDK deserialization still reads the original response.
                    this.LastPlan = await response.Content.ReadAsStringAsync(cancellationToken);
                }

                return response;
            }
        }
    }
}
