//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BenchmarkDotNet.Reports;
    using BenchmarkDotNet.Running;
    using Microsoft.Azure.Cosmos.Performance.Tests.Data;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.Azure.Cosmos.Routing;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Trace;

    class Program
    {
        static int Main(string[] args)
        {
            //CosmosDBConfiguration environmentConfiguration = ConfigurationService.Configuration;
            //Console.WriteLine($"Starting benchmark and dropping results on {environmentConfiguration.ReportsPath}.");
            //BenchmarkRunner.Run<ItemBenchmark>(new CustomBenchmarkConfiguration(environmentConfiguration));

            // The following flag is passed in via the gates to run the validation. This way local runs do not get blocked
            // on performance changes
            List<string> argsList = args != null ? new List<string>(args) : new List<string>();
            bool validateBaseline = argsList.Remove("--BaselineValidation");
            bool verifyFactory = argsList.Remove("--verify-pkrange-factory");
            bool verifySpike = argsList.Remove("--verify-spike");
            string[] updatedArgs = argsList.ToArray();

            if (verifyFactory)
            {
                return VerifyPkRangeFactory();
            }

            if (verifySpike)
            {
                return VerifySpike().GetAwaiter().GetResult();
            }

            using TracerProvider tracebuilder = Sdk.CreateTracerProviderBuilder()
                .AddSource("Azure.Cosmos.*")
                .AddCustomOtelExporter()
                .Build();

            using MeterProvider metricsBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter("Azure.Cosmos.Client.*")
                .AddReader(new PeriodicExportingMetricReader(exporter: new CustomMetricExporter(), 10000))
                .Build();

            if (validateBaseline)
            {
                SortedDictionary<string, double> operationToAllocatedMemory = new SortedDictionary<string, double>();

                // Run the test 3 times and average the results to help reduce any random variance in the results
                for(int i = 0; i < 3; i++)
                {
                    IEnumerable<Summary> summaries = BenchmarkSwitcher
                        .FromAssembly(typeof(Program).Assembly)
                        .Run(updatedArgs);

                    if (!PerformanceValidation.TryUpdateAllocatedMemoryAverage(summaries, operationToAllocatedMemory))
                    {
                        return -1;
                    }
                }

                return PerformanceValidation.ValidateSummaryResultsAgainstBaseline(operationToAllocatedMemory);
            }
            else
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                    .Run(updatedArgs);
            }

            return 0;
        }

        private static int VerifyPkRangeFactory()
        {
            try
            {
                const string tsvPath = "Data/shared_conversations_pkranges.tsv";
                IReadOnlyList<PartitionKeyRange> ranges = PkRangeRoutingFactory.LoadFromTsv(tsvPath);

                if (ranges.Count != PkRangeRoutingFactory.ExpectedRowCount)
                {
                    Console.Error.WriteLine($"FAIL: expected {PkRangeRoutingFactory.ExpectedRowCount} ranges, got {ranges.Count}.");
                    return 1;
                }

                // Build a complete routing map — validates boundary normalization + gap-free coverage.
                IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> tuples =
                    ranges.Select(r => Tuple.Create(r, (ServiceIdentity)null));
                CollectionRoutingMap map = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                    tuples,
                    collectionUniqueId: "verify-test",
                    useLengthAwareRangeComparer: false);
                if (map == null)
                {
                    PartitionKeyRange first = ranges.OrderBy(r => r.MinInclusive, StringComparer.Ordinal).First();
                    PartitionKeyRange last = ranges.OrderBy(r => r.MinInclusive, StringComparer.Ordinal).Last();
                    Console.Error.WriteLine(
                        $"FAIL: TryCreateCompleteRoutingMap returned null. first.min='{first.MinInclusive}' last.max='{last.MaxExclusive}'.");
                    return 1;
                }

                // Round-trip the /pkranges feed body.
                byte[] body = PkRangeRoutingFactory.SerializePkRangeFeedJson(ranges, "ccZ1ANCszwkDAAAAAAAAUA==");
                if (body == null || body.Length < 1000)
                {
                    Console.Error.WriteLine($"FAIL: serialized pkrange feed is suspiciously small ({body?.Length ?? 0} bytes).");
                    return 1;
                }

                string[] pool = PkRangeRoutingFactory.GenerateRandomPartitionKeyStrings(1024, seed: 42);
                string[] pool2 = PkRangeRoutingFactory.GenerateRandomPartitionKeyStrings(1024, seed: 42);
                if (!pool.SequenceEqual(pool2))
                {
                    Console.Error.WriteLine("FAIL: random PK pool is non-deterministic for identical seed.");
                    return 1;
                }

                Console.WriteLine($"OK: {ranges.Count} ranges, {map.OrderedPartitionKeyRanges.Count} in routing map, {body.Length} bytes serialized feed, {pool.Length} deterministic random PKs.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
        private static async System.Threading.Tasks.Task<int> VerifySpike()
        {
            // MockRequestHelper's static ctor reads samplepayload.json from CWD. Make sure CWD == output dir.
            string exeDir = System.IO.Path.GetDirectoryName(typeof(Program).Assembly.Location);
            if (!string.IsNullOrEmpty(exeDir))
            {
                System.IO.Directory.SetCurrentDirectory(exeDir);
            }

            // Suppress the VM-metadata IMDS probe — simpler than teaching the handler about 169.254.169.254.
            Environment.SetEnvironmentVariable("COSMOS_DISABLE_IMDS_ACCESS", "true");

            const string accountName = "spike";
            const string regionEndpoint = "https://spike-eastus.documents.azure.com";
            const string databaseName = "bench-db";
            const string containerName = "bench-coll";
            const string containerRid = "ccZ1ANCszwk=";

            // Small 3-range routing map — spike only needs to prove the mechanism.
            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>()
            {
                new PartitionKeyRange() { Id = "0", MinInclusive = "", MaxExclusive = "05C1DFFFFFFFFC", ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==" },
                new PartitionKeyRange() { Id = "1", MinInclusive = "05C1DFFFFFFFFC", MaxExclusive = "AA", ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==" },
                new PartitionKeyRange() { Id = "2", MinInclusive = "AA", MaxExclusive = "FF", ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==" }
            };

            Microsoft.Azure.Cosmos.Performance.Tests.Mocks.SpikeHttpHandler handler =
                new Microsoft.Azure.Cosmos.Performance.Tests.Mocks.SpikeHttpHandler(
                    accountName, regionEndpoint, databaseName, containerName, containerRid, ranges);
            Microsoft.Azure.Cosmos.Performance.Tests.Mocks.SpikeStubTransport transport =
                new Microsoft.Azure.Cosmos.Performance.Tests.Mocks.SpikeStubTransport();

            string fakeKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

            CosmosClientOptions options = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                HttpClientFactory = () => new System.Net.Http.HttpClient(handler, disposeHandler: false),
                TransportClientHandlerFactory = _ => transport,
            };

            try
            {
                using CosmosClient client = new CosmosClient(regionEndpoint + "/", fakeKey, options);
                Container container = client.GetContainer(databaseName, containerName);

                // "lets-benchmark" is the well-known id that MockRequestHelper.GetStoreResponse maps to 200 OK.
                using ResponseMessage response = await container.ReadItemStreamAsync(
                    id: "lets-benchmark",
                    partitionKey: new Cosmos.PartitionKey("lets-benchmark"));

                Console.WriteLine($"ReadItemStreamAsync -> {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"handler hits: account={handler.AccountHits} container={handler.ContainerHits} pkranges200={handler.PkRangesHits200} pkranges304={handler.PkRangesHits304} addresses={handler.AddressesHits} unknown={handler.UnknownUrls.Count}");
                Console.WriteLine($"transport InvokeStoreAsync calls: {transport.InvokeCount}");
                Console.WriteLine($"last transport: op={transport.LastOperationType} address='{transport.LastResourceAddress}' status={transport.LastReturnedStatus}");

                if (handler.UnknownUrls.Count > 0)
                {
                    Console.Error.WriteLine("FAIL: handler saw unexpected URLs:");
                    foreach (string u in handler.UnknownUrls) Console.Error.WriteLine("  " + u);
                    return 1;
                }

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.Error.WriteLine($"FAIL: expected 200 OK, got {response.StatusCode}.");
                    return 1;
                }

                if (handler.PkRangesHits200 != 1)
                {
                    Console.Error.WriteLine($"FAIL: expected exactly one 200 on /pkranges, got {handler.PkRangesHits200}.");
                    return 1;
                }

                if (transport.InvokeCount < 1)
                {
                    Console.Error.WriteLine("FAIL: transport stub was never invoked — SDK short-circuited before reaching RNTBD.");
                    return 1;
                }

                Console.WriteLine("OK: spike succeeded.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"handler hits: account={handler.AccountHits} container={handler.ContainerHits} pkranges200={handler.PkRangesHits200} pkranges304={handler.PkRangesHits304} addresses={handler.AddressesHits} unknown={handler.UnknownUrls.Count}");
                if (handler.UnknownUrls.Count > 0)
                {
                    foreach (string u in handler.UnknownUrls) Console.Error.WriteLine("  unknown: " + u);
                }
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
