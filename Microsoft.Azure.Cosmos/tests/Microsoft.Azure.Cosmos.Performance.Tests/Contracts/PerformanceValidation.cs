//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using BenchmarkDotNet.Reports;
    using Newtonsoft.Json;

    public class PerformanceValidation
    {
        public static bool TryUpdateAllocatedMemoryAverage(IEnumerable<Summary> summaries, Dictionary<string, double> operationToMemoryAllocated)
        {
            // If any of the operations have NA then something failed. Returning -1 will cause the gates to fail.
            foreach (Summary summary in summaries)
            {
                string[] content = summary.Table.Columns.First(x => string.Equals(@"Op/s", x.Header)).Content;
                foreach (string ops in content)
                {
                    if (string.Equals(@"NA", ops))
                    {
                        return false;
                    }
                }

                foreach (BenchmarkReport report in summary.Reports)
                {
                    double allocatedMemory = report.Metrics["Allocated Memory"].Value;
                    string operationName = report.BenchmarkCase.Descriptor.ToString() + ";" + string.Join(';', report.BenchmarkCase.Parameters.ValueInfo);
                    
                    // Average if the operation name already is in the dictionary
                    if(operationToMemoryAllocated.TryGetValue(operationName, out double value))
                    {
                        operationToMemoryAllocated[operationName] = (allocatedMemory + value)/2;
                    }
                    else
                    {
                        operationToMemoryAllocated.Add(operationName, allocatedMemory);
                    }
                }
            }

            return true;
        }

        public static int ValidateSummaryResultsAgainstBaseline(Dictionary<string, double> operationToMemoryAllocated)
        {
            // Using dotnet run in the gates puts the current directory at the root of the github project rather than the execute folder.
            string currentDirectory = Directory.GetCurrentDirectory();
            int removePathsLowerThanIndex = currentDirectory.IndexOf(@"\Microsoft.Azure.Cosmos");
            if(removePathsLowerThanIndex >= 0)
            {
                currentDirectory = currentDirectory.Remove(removePathsLowerThanIndex);
            }
            
            currentDirectory += @"\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Performance.Tests\bin\Release\netcoreapp3.1\Contracts\";

            // Always write the updated version. This will change with each run.
            string currentBenchmarkResults = JsonConvert.SerializeObject(operationToMemoryAllocated, Formatting.Indented);
            File.WriteAllText(currentDirectory + "CurrentBenchmarkResults.json", currentBenchmarkResults);

            string baselineJson = File.ReadAllText(currentDirectory + "BenchmarkResults.json");
            Dictionary<string, double> baselineBenchmarkResults = JsonConvert.DeserializeObject<Dictionary<string, double>>(baselineJson);

            if (baselineBenchmarkResults.Count != operationToMemoryAllocated.Count)
            {
                Console.WriteLine("CurrentBenchmarkResults count does not match the baseline BenchmarkResults.json. Please update the BenchmarkResults.json: " + currentBenchmarkResults);
                return 1;
            }

            foreach(KeyValuePair<string, double> currentResult in operationToMemoryAllocated)
            {
                double baselineResult = baselineBenchmarkResults[currentResult.Key];

                // Add 5% buffer to avoid minor variation between test runs
                double diff = currentResult.Value - baselineResult;
                double maxAllowedDiff = baselineResult * .05;
                if (diff > maxAllowedDiff)
                {
                    Console.WriteLine("The current results have exceed the baseline memory allocations. Please fix the performance regression. " +
                        "If this is by design please update the BenchmarkResults.json file using the CurrentBenchmarkResults.json in the output folder: " + currentBenchmarkResults);
                    return 1;
                }
                else if (-diff > maxAllowedDiff)
                {
                    Console.WriteLine("The current results show a performance improvement. " +
                        "Please update the BenchmarkResults.json file using the CurrentBenchmarkResults.json in the output folder: " + currentBenchmarkResults);
                    return 1;
                }
            }

            return 0;
        }
    }
}
