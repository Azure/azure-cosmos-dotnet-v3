//-----------------------------------------------------------------------
// <copyright file="JsonPerformanceTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Functional")]
    public class JsonPerformanceTests
    {
        private static readonly bool runPerformanceTests = false;

        [TestInitialize]
        public void TestInitialize()
        {
            // Put test init code here
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            // put class init code here
        }

        [TestMethod]
        [Owner("brchon")]
        public void CombinedScriptsDataBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("CombinedScriptsData.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void CountriesBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("countries");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void DevTestCollBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("devtestcoll.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void LastFMBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("lastfm");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void LogDataBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("LogData.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void MillionSong1KDocumentsBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("MillionSong1KDocuments.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void MsnCollectionBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("MsnCollection.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void NutritionDataBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("NutritionData");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("runsCollection");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesCommitteesBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("states_committees.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesLegislatorsBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("states_legislators");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Store01Benchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("store01C.json");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TicinoErrorBucketsBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("TicinoErrorBuckets");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("twitter_data");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Benchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("ups1");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsBenchmark()
        {
            if (runPerformanceTests)
            {
                this.PerformanceBenchmarkCuratedJson("XpertEvents");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Utf8VsUtf16StringWrite()
        {
            void RunPerf(string utf16String, JsonSerializationFormat jsonSerializationFormat, bool useUtf8)
            {
                ReadOnlySpan<byte> utf8String = Encoding.UTF8.GetBytes(utf16String);

                Stopwatch stopWatch = new Stopwatch();
                for (int i = 0; i < 1000000; i++)
                {
                    IJsonWriter writer = JsonWriter.Create(jsonSerializationFormat);
                    stopWatch.Start();
                    if (useUtf8)
                    {
                        writer.WriteStringValue(utf8String);
                    }
                    else
                    {
                        writer.WriteStringValue(utf16String);
                    }
                    stopWatch.Stop();
                }

                Console.WriteLine($"UTF {(useUtf8 ? 8 : 16)} {jsonSerializationFormat} writer + string length: {utf16String.Length} = {stopWatch.ElapsedMilliseconds} ms");
            }

            foreach (int stringLength in new int[] { 8, 32, 256, 1024, 4096 })
            {
                foreach (JsonSerializationFormat jsonSerializationFormat in new JsonSerializationFormat[] { JsonSerializationFormat.Text, JsonSerializationFormat.Binary })
                {
                    foreach (bool useUtf8 in new bool[] { false, true })
                    {
                        RunPerf(
                            utf16String: new string('a', stringLength),
                            jsonSerializationFormat: jsonSerializationFormat,
                            useUtf8: useUtf8);
                    }
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Utf8VsUtf16StringRead()
        {
            void RunPerf(string utf16String, JsonSerializationFormat jsonSerializationFormat, bool useUtf8)
            {
                Stopwatch stopWatch = new Stopwatch();
                byte[] payload = JsonTestUtils.ConvertTextToBinary("\"" + utf16String + "\"");
                for (int i = 0; i < 1000000; i++)
                {
                    IJsonReader reader = JsonReader.Create(payload);
                    reader.Read();
                    stopWatch.Start();
                    if (useUtf8)
                    {
                        reader.TryGetBufferedUtf8StringValue(out ReadOnlyMemory<byte> bufferedUtf8StringValue);
                    }
                    else
                    {
                        string value = reader.GetStringValue();
                    }
                    stopWatch.Stop();
                }

                Console.WriteLine($"UTF {(useUtf8 ? 8 : 16)} {jsonSerializationFormat} reader + string length: {utf16String.Length} = {stopWatch.ElapsedMilliseconds} ms");
            }

            foreach (int stringLength in new int[] { 8, 32, 256, 1024, 4096 })
            {
                foreach (JsonSerializationFormat jsonSerializationFormat in new JsonSerializationFormat[] { JsonSerializationFormat.Text, JsonSerializationFormat.Binary })
                {
                    foreach (bool useUtf8 in new bool[] { false, true })
                    {
                        RunPerf(
                            utf16String: new string('a', stringLength),
                            jsonSerializationFormat: jsonSerializationFormat,
                            useUtf8: useUtf8);
                    }
                }
            }
        }

        private void PerformanceBenchmarkCuratedJson(string path)
        {
            path = string.Format("TestJsons/{0}", path);
            string json = TextFileConcatenation.ReadMultipartFile(path);
            const int numberOfIterations = 1;

            JsonPerfMeasurement.MeasurePerf(json, path, numberOfIterations);
        }
    }
}
