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
        private const string BaselineBenchmarkResultsFileName = "BenchmarkResults.json";
        private const string CurrentBenchmarkResultsFileName = "CurrentBenchmarkResults.json";

#if DEBUG
        private const string DirectoryPath = @"\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Performance.Tests\bin\Debug\netcoreapp3.1\Contracts\";
#else
        private const string DirectoryPath =  @"\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Performance.Tests\bin\Release\netcoreapp3.1\Contracts\";
#endif
        

        private static readonly string UpdateMessage = $"Please update the Microsoft.Azure.Cosmos.Performance.Tests\\Contracts\\{PerformanceValidation.BaselineBenchmarkResultsFileName} " +
            $" file by using the following results found at {PerformanceValidation.DirectoryPath}\\{PerformanceValidation.CurrentBenchmarkResultsFileName} or by using: ";

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

            currentDirectory += PerformanceValidation.DirectoryPath;

            // Always write the updated version. This will change with each run.
            string currentBenchmarkResults = JsonConvert.SerializeObject(operationToMemoryAllocated, Formatting.Indented);
            File.WriteAllText(currentDirectory + PerformanceValidation.CurrentBenchmarkResultsFileName, currentBenchmarkResults);

            string baselineJson = File.ReadAllText(currentDirectory + PerformanceValidation.BaselineBenchmarkResultsFileName);
            Dictionary<string, double> baselineBenchmarkResults = JsonConvert.DeserializeObject<Dictionary<string, double>>(baselineJson);

            if (baselineBenchmarkResults.Count != operationToMemoryAllocated.Count)
            {
                Console.WriteLine(PerformanceValidation.UpdateMessage + currentBenchmarkResults);
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
                    Console.WriteLine(PerformanceValidation.UpdateMessage + currentBenchmarkResults);
                    return 1;
                }
                else if (-diff > maxAllowedDiff)
                {
                    Console.WriteLine(PerformanceValidation.UpdateMessage + currentBenchmarkResults);
                    return 1;
                }
            }

            Console.WriteLine("Current benchmark results: " + currentBenchmarkResults);

            return 0;
        }
    }
}
