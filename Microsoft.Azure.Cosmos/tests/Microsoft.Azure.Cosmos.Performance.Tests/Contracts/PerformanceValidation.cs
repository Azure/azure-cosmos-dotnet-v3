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

        public static bool TryUpdateAllocatedMemoryAverage(IEnumerable<Summary> summaries, SortedDictionary<string, double> operationToMemoryAllocated)
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

        public static int ValidateSummaryResultsAgainstBaseline(SortedDictionary<string, double> operationToMemoryAllocated)
        {
            // Using dotnet run in the gates puts the current directory at the root of the github project rather than the execute folder.
            string currentDirectory = Directory.GetCurrentDirectory();
            int removePathsLowerThanIndex = currentDirectory.IndexOf(@"\Microsoft.Azure.Cosmos");
            if(removePathsLowerThanIndex >= 0)
            {
                currentDirectory = currentDirectory.Remove(removePathsLowerThanIndex);
            }

            currentDirectory += PerformanceValidation.DirectoryPath;

            string baselineJson = File.ReadAllText(currentDirectory + PerformanceValidation.BaselineBenchmarkResultsFileName);
            Dictionary<string, double> baselineBenchmarkResults = JsonConvert.DeserializeObject<Dictionary<string, double>>(baselineJson);

            List<string> failures = new List<string>();
            SortedDictionary<string, double> updatedBaseline = new SortedDictionary<string, double>();
            foreach (KeyValuePair<string, double> currentResult in operationToMemoryAllocated)
            {
                if(!baselineBenchmarkResults.TryGetValue(
                    currentResult.Key, 
                    out double baselineResult))
                {
                    updatedBaseline.Add(currentResult.Key, currentResult.Value);
                    continue;
                }

                // Add 5% buffer to avoid minor variation between test runs
                double diff = Math.Abs(currentResult.Value - baselineResult);
                double maxAllowedDiff = baselineResult * .05;
                double minDiffToUpdatebaseLine = baselineResult * .02;
                if (diff > maxAllowedDiff)
                {
                    updatedBaseline.Add(currentResult.Key, currentResult.Value);
                    failures.Add($"{currentResult.Key}: {currentResult.Value}");
                }
                else if(diff > minDiffToUpdatebaseLine)
                {
                    // Update the value if it is greater than 2% difference.
                    // This reduces the noise and make it easier to see which values actually changed
                    updatedBaseline.Add(currentResult.Key, currentResult.Value);
                }
                else
                {
                    // Use the baseline if the value didn't change by more than 2% to avoid updating values unnecessarily
                    // This makes it easier to see which values actually need to be updated.
                    updatedBaseline.Add(currentResult.Key, baselineResult);
                }
            }

            // Always write the updated version. This will change with each run.
            string currentBenchmarkResults = JsonConvert.SerializeObject(updatedBaseline, Formatting.Indented);
            File.WriteAllText(currentDirectory + PerformanceValidation.CurrentBenchmarkResultsFileName, currentBenchmarkResults);
            Console.WriteLine("Current benchmark results: " + currentBenchmarkResults);

            if (failures.Any())
            {
                Console.WriteLine(PerformanceValidation.UpdateMessage);
                foreach(string failure in failures)
                {
                    Console.WriteLine(failure);
                }
                
                return 1;
            }

            return 0;
        }
    }
}
