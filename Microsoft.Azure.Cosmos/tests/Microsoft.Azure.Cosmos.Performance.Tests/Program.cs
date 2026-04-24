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
            string[] updatedArgs = argsList.ToArray();

            if (verifyFactory)
            {
                return VerifyPkRangeFactory();
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
    }
}
