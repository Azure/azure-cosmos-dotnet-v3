//-----------------------------------------------------------------------
// <copyright file="JsonNavigatorTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Json;
    using System.IO;
    using System.Globalization;

    [TestClass]
    [TestCategory("Functional")]
    public class JsonNavigatorTests
    {
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

        #region SimpleTests
        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            string input = "true";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string input = "1337";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.0";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string input = "-1337.0";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string input = "6.02252e23";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberRegressionTest()
        {
            // regression test - the value 0.00085647800000000004 was being incorrectly rejected
            string numberValueString = "0.00085647800000000004";

            this.VerifyNavigator(numberValueString);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string input = "\"Hello World\"";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1, -7.3e-2, 77.0001e90 ]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.0, -1, -1.0, 1, 2, \"hello\", null, true, false]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StrangeNumberArrayTest()
        {
            string input = @"[
                1111111110111111111011111111101111111110,
                1111111110111111111011111111101111111110111111111011111111101111111110,
               11111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110,
                1111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110
                    ]";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            string input = "{}";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            this.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitivesObjectTest()
        {
            string input = @"{
                        ""id"": ""7029d079-4016-4436-b7da-36c0bae54ff6"",
                        ""double"": 0.18963001816981939,
                        ""int"": -1330192615,
                        ""string"": ""XCPCFXPHHF"",
                        ""boolean"": true,
                        ""null"": null,
                        ""datetime"": ""2526-07-11T18:18:16.4520716"",
                        ""spatialPoint"": {
                            ""type"": ""Point"",
                            ""coordinates"": [
                                118.9897,
                                -46.6781
                            ]
                        },
                        ""text"": ""tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby""
                    }";

            this.VerifyNavigator(input);
        }
        #endregion

        #region CurratedDocs
        [TestMethod]
        [Owner("brchon")]
        public void CombinedScriptsDataTest()
        {
            this.VerifyNavigatorWithCurratedDoc("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void CountriesTest()
        {
            this.VerifyNavigatorWithCurratedDoc("countries.json", false);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DevTestCollTest()
        {
            this.VerifyNavigatorWithCurratedDoc("devtestcoll.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LastFMTest()
        {
            this.VerifyNavigatorWithCurratedDoc("lastfm.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LogDataTest()
        {
            this.VerifyNavigatorWithCurratedDoc("LogData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MillionSong1KDocumentsTest()
        {
            this.VerifyNavigatorWithCurratedDoc("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MsnCollectionTest()
        {
            this.VerifyNavigatorWithCurratedDoc("MsnCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void NutritionDataTest()
        {
            this.VerifyNavigatorWithCurratedDoc("NutritionData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            this.VerifyNavigatorWithCurratedDoc("runsCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesCommitteesTest()
        {
            this.VerifyNavigatorWithCurratedDoc("states_committees.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesLegislatorsTest()
        {
            this.VerifyNavigatorWithCurratedDoc("states_legislators.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Store01Test()
        {
            this.VerifyNavigatorWithCurratedDoc("store01C.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TicinoErrorBucketsTest()
        {
            this.VerifyNavigatorWithCurratedDoc("TicinoErrorBuckets.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            this.VerifyNavigatorWithCurratedDoc("twitter_data.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            this.VerifyNavigatorWithCurratedDoc("ups1.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            this.VerifyNavigatorWithCurratedDoc("XpertEvents.json");
        }

        private void VerifyNavigatorWithCurratedDoc(string filename, bool performExtraChecks = true)
        {
            string path = string.Format("TestJsons/{0}", filename);
            string json = File.ReadAllText(path);
#if true
            json = JsonTestUtils.RandomSampleJson(json);
#endif

            this.VerifyNavigator(json, performExtraChecks);
        }
        #endregion

        private void VerifyNavigator(string input, bool performExtraChecks = true)
        {
            this.VerifyNavigator(input, null, performExtraChecks);
        }

        private void VerifyNavigator(string input, Exception expectedException, bool performExtraChecks = true)
        {
            CultureInfo defaultCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

            CultureInfo[] cultureInfoList = new CultureInfo[]
            {
                defaultCultureInfo,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR")
            };

            try
            {
                foreach (CultureInfo cultureInfo in cultureInfoList)
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

                    IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(input));
                    JsonTokenInfo[] tokensFromReader = JsonNavigatorTests.GetTokensWithReader(jsonReader);

                    // Test text
                    IJsonNavigator textNavigator = JsonNavigator.Create(Encoding.UTF8.GetBytes(input));
                    IJsonNavigatorNode textRootNode = textNavigator.GetRootNode();
                    JsonTokenInfo[] tokensFromTextNavigator = JsonNavigatorTests.GetTokensFromNode(textRootNode, textNavigator, performExtraChecks);

                    Assert.IsTrue(tokensFromTextNavigator.SequenceEqual(tokensFromReader));

                    // Test binary
                    byte[] binaryInput = JsonTestUtils.ConvertTextToBinary(input);
                    IJsonNavigator binaryNavigator = JsonNavigator.Create(binaryInput);
                    IJsonNavigatorNode binaryRootNode = binaryNavigator.GetRootNode();
                    JsonTokenInfo[] tokensFromBinaryNavigator = JsonNavigatorTests.GetTokensFromNode(binaryRootNode, binaryNavigator, performExtraChecks);

                    Assert.IsTrue(tokensFromBinaryNavigator.SequenceEqual(tokensFromReader));

                    // Test binary + user string encoding
                    JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 4096);
                    byte[] binaryWithUserStringEncodingInput = JsonTestUtils.ConvertTextToBinary(input, jsonStringDictionary);
                    if (jsonStringDictionary.TryGetStringAtIndex(index: 0, value: out string temp))
                    {
                        Assert.IsFalse(binaryWithUserStringEncodingInput.SequenceEqual(binaryInput), "Binary should be different with user string encoding");
                    }
                    IJsonNavigator binaryNavigatorWithUserStringEncoding = JsonNavigator.Create(binaryInput, jsonStringDictionary);
                    IJsonNavigatorNode binaryRootNodeWithUserStringEncoding = binaryNavigatorWithUserStringEncoding.GetRootNode();
                    JsonTokenInfo[] tokensFromBinaryNavigatorWithUserStringEncoding = JsonNavigatorTests.GetTokensFromNode(binaryRootNode, binaryNavigator, performExtraChecks);

                    Assert.IsTrue(tokensFromBinaryNavigatorWithUserStringEncoding.SequenceEqual(tokensFromReader));
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        internal static JsonTokenInfo[] GetTokensWithReader(IJsonReader jsonReader)
        {
            List<JsonTokenInfo> tokens = new List<JsonTokenInfo>();
            while (jsonReader.Read())
            {
                switch (jsonReader.CurrentTokenType)
                {
                    case JsonTokenType.NotStarted:
                        throw new InvalidOperationException();
                    case JsonTokenType.BeginArray:
                        tokens.Add(JsonTokenInfo.ArrayStart());
                        break;
                    case JsonTokenType.EndArray:
                        tokens.Add(JsonTokenInfo.ArrayEnd());
                        break;
                    case JsonTokenType.BeginObject:
                        tokens.Add(JsonTokenInfo.ObjectStart());
                        break;
                    case JsonTokenType.EndObject:
                        tokens.Add(JsonTokenInfo.ObjectEnd());
                        break;
                    case JsonTokenType.String:
                        tokens.Add(JsonTokenInfo.String(jsonReader.GetStringValue()));
                        break;
                    case JsonTokenType.Number:
                        tokens.Add(JsonTokenInfo.Number(jsonReader.GetNumberValue()));
                        break;
                    case JsonTokenType.True:
                        tokens.Add(JsonTokenInfo.Boolean(true));
                        break;
                    case JsonTokenType.False:
                        tokens.Add(JsonTokenInfo.Boolean(false));
                        break;
                    case JsonTokenType.Null:
                        tokens.Add(JsonTokenInfo.Null());
                        break;
                    case JsonTokenType.FieldName:
                        tokens.Add(JsonTokenInfo.FieldName(jsonReader.GetStringValue()));
                        break;
                    default:
                        break;
                }
            }

            return tokens.ToArray();
        }

        internal static JsonTokenInfo[] GetTokensFromNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            switch (navigator.GetNodeType(node))
            {
                case JsonNodeType.Null:
                    return new JsonTokenInfo[] { JsonTokenInfo.Null() };
                case JsonNodeType.False:
                    return new JsonTokenInfo[] { JsonTokenInfo.Boolean(false) };
                case JsonNodeType.True:
                    return new JsonTokenInfo[] { JsonTokenInfo.Boolean(true) };
                case JsonNodeType.Number:
                    return new JsonTokenInfo[] { JsonTokenInfo.Number(navigator.GetNumberValue(node)) };
                case JsonNodeType.String:
                    return new JsonTokenInfo[] { JsonTokenInfo.String(navigator.GetStringValue(node)) };
                case JsonNodeType.Array:
                    return JsonNavigatorTests.GetTokensFromArrayNode(node, navigator, performCorrectnessCheck);
                case JsonNodeType.Object:
                    return JsonNavigatorTests.GetTokensFromObjectNode(node, navigator, performCorrectnessCheck);
                case JsonNodeType.FieldName:
                    return new JsonTokenInfo[] { JsonTokenInfo.FieldName(navigator.GetStringValue(node)) };
                default:
                    throw new InvalidOperationException();
            }
        }

        internal static JsonTokenInfo[] GetTokensFromObjectNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            // Get the tokens through .GetObjectProperties
            List<JsonTokenInfo> tokensFromGetProperties = new List<JsonTokenInfo>();
            IEnumerable<ObjectProperty> properties = navigator.GetObjectProperties(node);

            tokensFromGetProperties.Add(JsonTokenInfo.ObjectStart());
            foreach (ObjectProperty property in properties)
            {
                string fieldname = navigator.GetStringValue(property.NameNode);
                tokensFromGetProperties.Add(JsonTokenInfo.FieldName(fieldname));
                tokensFromGetProperties.AddRange(JsonNavigatorTests.GetTokensFromNode(property.ValueNode, navigator, performCorrectnessCheck));
            }
            tokensFromGetProperties.Add(JsonTokenInfo.ObjectEnd());

            if (performCorrectnessCheck)
            {
                // Get the tokens again through .TryGetObjectProperty
                List<JsonTokenInfo> tokensFromTryGetProperty = new List<JsonTokenInfo>();

                tokensFromTryGetProperty.Add(JsonTokenInfo.ObjectStart());
                foreach (ObjectProperty objectProperty in properties)
                {
                    ObjectProperty propertyFromTryGetProperty;
                    string fieldname = navigator.GetStringValue(objectProperty.NameNode);
                    if (navigator.TryGetObjectProperty(node, fieldname, out propertyFromTryGetProperty))
                    {
                        tokensFromTryGetProperty.Add(JsonTokenInfo.FieldName(fieldname));
                        tokensFromTryGetProperty.AddRange(JsonNavigatorTests.GetTokensFromNode(propertyFromTryGetProperty.ValueNode, navigator, performCorrectnessCheck));
                    }
                    else
                    {
                        Assert.Fail($"Failed to get object property with name: {fieldname}");
                    }
                }
                tokensFromTryGetProperty.Add(JsonTokenInfo.ObjectEnd());
                Assert.AreEqual(properties.Count(), navigator.GetObjectPropertyCount(node));
                Assert.IsTrue(tokensFromGetProperties.SequenceEqual(tokensFromTryGetProperty));
            }

            return tokensFromGetProperties.ToArray();
        }

        internal static JsonTokenInfo[] GetTokensFromArrayNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            // Get tokens once through IEnumerable
            List<JsonTokenInfo> tokensFromIEnumerable = new List<JsonTokenInfo>();
            IEnumerable<IJsonNavigatorNode> arrayItems = navigator.GetArrayItems(node);

            tokensFromIEnumerable.Add(JsonTokenInfo.ArrayStart());
            foreach (IJsonNavigatorNode arrayItem in arrayItems)
            {
                tokensFromIEnumerable.AddRange(JsonNavigatorTests.GetTokensFromNode(arrayItem, navigator, performCorrectnessCheck));
            }

            tokensFromIEnumerable.Add(JsonTokenInfo.ArrayEnd());

            if (performCorrectnessCheck)
            {
                // Get tokens once again through indexer
                List<JsonTokenInfo> tokensFromIndexer = new List<JsonTokenInfo>();
                tokensFromIndexer.Add(JsonTokenInfo.ArrayStart());
                for (int i = 0; i < navigator.GetArrayItemCount(node); ++i)
                {
                    tokensFromIndexer.AddRange(JsonNavigatorTests.GetTokensFromNode(navigator.GetArrayItemAt(node, i), navigator, performCorrectnessCheck));
                }

                tokensFromIndexer.Add(JsonTokenInfo.ArrayEnd());

                Assert.AreEqual(arrayItems.Count(), navigator.GetArrayItemCount(node));
                Assert.IsTrue(tokensFromIEnumerable.SequenceEqual(tokensFromIndexer));

                try
                {
                    navigator.GetArrayItemAt(node, navigator.GetArrayItemCount(node) + 1);
                    Assert.Fail("Expected to get an index out of range exception from going one past the end of the array.");
                }
                catch (IndexOutOfRangeException)
                {
                    Assert.AreEqual(navigator.SerializationFormat, JsonSerializationFormat.Binary);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Assert.AreEqual(navigator.SerializationFormat, JsonSerializationFormat.Text);
                }
            }

            return tokensFromIEnumerable.ToArray();
        }
    }
}