//-----------------------------------------------------------------------
// <copyright file="LazyCosmosElementTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests for LazyCosmosElementTests.
    /// </summary>
    [TestClass]
    public class CosmosElementJsonConverterTests
    {
        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            string input = "true";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string input = "1337";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.1337";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string input = "\"Hello World\"";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayTest()
        {
            string input = "[-2,-1,0,1,2]";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\":\"example glossary\"}";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void CombinedScriptsDataTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void DevTestCollTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("devtestcoll.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LastFMTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("lastfm.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LogDataTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("LogData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MillionSong1KDocumentsTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MsnCollectionTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("MsnCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void NutritionDataTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("NutritionData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("runsCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesCommitteesTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("states_committees.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesLegislatorsTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("states_legislators.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Store01Test()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("store01C.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TicinoErrorBucketsTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("TicinoErrorBuckets.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("twitter_data.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("ups1.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            CosmosElementJsonConverterTests.TestRoundtripFile("XpertEvents.json");
        }

        private static void TestRoundtripFile(string filename)
        {
            string payload = CosmosElementJsonConverterTests.GetPayload(filename);
            CosmosElementJsonConverterTests.VerifyConverter(payload);
        }

        private static string NewtonsoftFormat(string json)
        {
            return JsonConvert.SerializeObject(JToken.Parse(json), Formatting.None);
        }

        private static void VerifyConverter(string input)
        {
            input = CosmosElementJsonConverterTests.NewtonsoftFormat(input);
            CosmosElement cosmosElement = JsonConvert.DeserializeObject<CosmosElement>(input, new CosmosElementJsonConverter());
            string toString = JsonConvert.SerializeObject(cosmosElement, new CosmosElementJsonConverter());
            Assert.AreEqual(input, toString);
        }

        private static string GetPayload(string filename)
        {
            string path = string.Format("TestJsons/{0}", filename);
            string json = File.ReadAllText(path);

            IEnumerable<object> documents;
            try
            {
                documents = JsonConvert.DeserializeObject<List<object>>(json);
            }
            catch (JsonSerializationException)
            {
                documents = new List<object>
                {
                    JsonConvert.DeserializeObject<object>(json)
                };
            }

            documents = documents.OrderBy(x => Guid.NewGuid()).Take(1);

            json = JsonConvert.SerializeObject(documents);
            return json;
        }
    }
}
