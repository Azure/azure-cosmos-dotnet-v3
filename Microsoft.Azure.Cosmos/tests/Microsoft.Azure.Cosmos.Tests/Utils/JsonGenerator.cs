//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.IO;

    internal class JsonGenerator
    {
        private static readonly Random RandomGenerator = new();

        public static async Task<string> CreateComplexJsonFileAsync(string filePath)
        {
            Dictionary<string, object> jsonData = GenerateComplexJsonData();

            // Write JSON to a file
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(jsonData, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));

            return filePath;
        }

        public static void DeleteJsonFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static Dictionary<string, object> GenerateComplexJsonData()
        {
            return new Dictionary<string, object>
            {
                ["Id"] = Guid.NewGuid(),
                ["Reference"] = GenerateReference(),
                ["CreatedOn"] = DateTime.UtcNow.ToString("o"),
                ["HexCode"] = GenerateHexColor(),
                ["NumericSequence"] = GenerateRandomSequence(5, 100, 999),
                ["Names"] = GenerateNames(5),
                ["NestedObject"] = new
                {
                    Meta = "Metadata Info",
                    Timestamps = GenerateDateRange(DateTime.UtcNow, -5, 5),
                    DeepHierarchy = GenerateNestedHierarchy(4)
                },
                ["ComplexArray"] = GenerateComplexArray(6),
                ["KeyValuePairs"] = GenerateKeyValuePairs(4),
                ["MixedData"] = GenerateMixedDataArray()
            };
        }

        private static string GenerateReference()
        {
            return $"REF-{Guid.NewGuid():N}"[..8];
        }

        private static string GenerateHexColor()
        {
            return $"#{RandomGenerator.Next(0x1000000):X6}";
        }

        private static int[] GenerateRandomSequence(int count, int min, int max)
        {
            int[] numbers = new int[count];
            for (int i = 0; i < count; i++)
            {
                numbers[i] = RandomGenerator.Next(min, max);
            }
            return numbers;
        }

        private static string[] GenerateNames(int count)
        {
            string[] sampleNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Heidi" };
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = sampleNames[RandomGenerator.Next(sampleNames.Length)];
            }
            return names;
        }

        private static DateTime[] GenerateDateRange(DateTime baseDate, int daysBefore, int daysAfter)
        {
            DateTime[] dates = new DateTime[daysBefore + daysAfter + 1];
            for (int i = -daysBefore; i <= daysAfter; i++)
            {
                dates[i + daysBefore] = baseDate.AddDays(i);
            }
            return dates;
        }

        private static List<object> GenerateComplexArray(int count)
        {
            List<object> complexArray = new List<object>();
            for (int i = 0; i < count; i++)
            {
                complexArray.Add(new
                {
                    Key = $"Key-{RandomGenerator.Next(1, 100)}",
                    Value = RandomGenerator.NextDouble() * 1000,
                    Timestamp = DateTime.UtcNow.AddMinutes(-RandomGenerator.Next(1, 100))
                });
            }
            return complexArray;
        }

        private static Dictionary<string, object> GenerateNestedHierarchy(int levels)
        {
            if (levels <= 0) return null;

            return new Dictionary<string, object>
            {
                ["Level"] = levels,
                ["Description"] = $"Details at Level {levels}",
                ["Next"] = GenerateNestedHierarchy(levels - 1),
                ["Attributes"] = new
                {
                    Active = RandomGenerator.Next(0, 2) == 1,
                    Priority = RandomGenerator.Next(1, 10)
                }
            };
        }

        private static Dictionary<string, string> GenerateKeyValuePairs(int count)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
            for (int i = 0; i < count; i++)
            {
                keyValuePairs[$"Key{i}"] = $"Value{RandomGenerator.Next(100, 1000)}";
            }
            return keyValuePairs;
        }

        private static List<object> GenerateMixedDataArray()
        {
            return new List<object>
            {
                Guid.NewGuid(),
                RandomGenerator.Next(1, 1000),
                $"RandomString-{Guid.NewGuid():N}"[..12],
                DateTime.UtcNow.ToString("o"),
                new { NestedKey = "NestedValue", NestedId = Guid.NewGuid() }
            };
        }
    }
}
