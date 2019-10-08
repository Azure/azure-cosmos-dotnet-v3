//-----------------------------------------------------------------------
// <copyright file="JsonPerformanceTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System.IO;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Functional")]
    public class JsonPerformanceTests
    {
        private static readonly bool runPerformanceTests = true;

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

        private void PerformanceBenchmarkCuratedJson(string path)
        {
            path = string.Format("TestJsons/{0}", path);
            string json = TextFileConcatenation.ReadMultipartFile(path);
            const int numberOfIterations = 1;

            JsonPerfMeasurement.MeasurePerf(json, path, numberOfIterations);
        }
    }
}
