//-----------------------------------------------------------------------
// <copyright file="JsonMicroBenchmarks.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Functional")]
    public class JsonMicroBenchmarks
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
        public void NullMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string nullString = "null";
                this.ExecuteMicroBenchmark(nullString, "Null");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TrueMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string trueString = "true";
                this.ExecuteMicroBenchmark(trueString, "True");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string falseString = "false";
                this.ExecuteMicroBenchmark(falseString, "False");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntegerMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string integerString = "123";
                this.ExecuteMicroBenchmark(integerString, "Integer");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string doubleString = "6.0221409e+23";
                this.ExecuteMicroBenchmark(doubleString, "Double");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string arrayString = "[]";
                this.ExecuteMicroBenchmark(arrayString, "Array");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string objectString = "{}";
                this.ExecuteMicroBenchmark(objectString, "Object");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringMicroBenchmark()
        {
            if (runPerformanceTests)
            {
                string stringString = "\"Hello World\"";
                this.ExecuteMicroBenchmark(stringString, "String");
            }
        }

        private void ExecuteMicroBenchmark(string token, string microName)
        {
            List<string> tokenArray = new List<string>();
            for (int i = 0; i < 1000000; i++)
            {
                tokenArray.Add(token);
            }

            string tokenArrayJson = "[" + string.Join(",", tokenArray) + "]";

            JsonPerfMeasurement.MeasurePerf(tokenArrayJson, microName + "Micro Benchmark");
        }
    }
}
