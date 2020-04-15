//-----------------------------------------------------------------------
// <copyright file="JsonNavigatorTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Json;
    using System.IO;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Tests;

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

        [TestMethod]
        [Owner("brchon")]
        public void Int8Test()
        {
            sbyte[] values = new sbyte[] { sbyte.MinValue, sbyte.MinValue + 1, -1, 0, 1, sbyte.MaxValue, sbyte.MaxValue - 1 };
            foreach (sbyte value in values)
            {
                string input = $"I{value}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int16Test()
        {
            short[] values = new short[] { short.MinValue, short.MinValue + 1, -1, 0, 1, short.MaxValue, short.MaxValue - 1 };
            foreach (short value in values)
            {
                string input = $"H{value}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int32Test()
        {
            int[] values = new int[] { int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue, int.MaxValue - 1 };
            foreach (int value in values)
            {
                string input = $"L{value}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int64Test()
        {
            long[] values = new long[] { long.MinValue, long.MinValue + 1, -1, 0, 1, long.MaxValue, long.MaxValue - 1 };
            foreach (long value in values)
            {
                string input = $"LL{value}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void UInt32Test()
        {
            uint[] values = new uint[] { uint.MinValue, uint.MinValue + 1, 0, 1, uint.MaxValue, uint.MaxValue - 1 };
            foreach (uint value in values)
            {
                string input = $"UL{value}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Float32Test()
        {
            float[] values = new float[] { float.MinValue, float.MinValue + 1, 0, 1, float.MaxValue, float.MaxValue - 1 };
            foreach (float value in values)
            {
                string input = $"S{value.ToString("G9", CultureInfo.InvariantCulture)}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Float64Test()
        {
            double[] values = new double[] { double.MinValue, double.MinValue + 1, 0, 1, double.MaxValue, double.MaxValue - 1 };
            foreach (double value in values)
            {
                string input = $"D{value.ToString("G17", CultureInfo.InvariantCulture)}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void GuidTest()
        {
            Guid[] values = new Guid[] { Guid.Empty, Guid.NewGuid() };
            foreach (Guid value in values)
            {
                string input = $"G{value.ToString()}";
                this.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void BinaryTest()
        {
            {
                // Empty Binary
                string input = $"B";
                this.VerifyNavigator(input);
            }

            {
                // Binary 1 Byte Length
                IReadOnlyList<byte> binary = Enumerable.Range(0, 25).Select(x => (byte)x).ToList();
                string input = $"B{Convert.ToBase64String(binary.ToArray())}";
                this.VerifyNavigator(input);
            }
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
            this.VerifyNavigatorWithCurratedDoc("countries", false);
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
            this.VerifyNavigatorWithCurratedDoc("lastfm");
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
            this.VerifyNavigatorWithCurratedDoc("NutritionData");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            this.VerifyNavigatorWithCurratedDoc("runsCollection");
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
            this.VerifyNavigatorWithCurratedDoc("states_legislators");
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
            this.VerifyNavigatorWithCurratedDoc("TicinoErrorBuckets");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            this.VerifyNavigatorWithCurratedDoc("twitter_data");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            this.VerifyNavigatorWithCurratedDoc("ups1");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            this.VerifyNavigatorWithCurratedDoc("XpertEvents");
        }

        private void VerifyNavigatorWithCurratedDoc(string path, bool performExtraChecks = true)
        {
            path = string.Format("TestJsons/{0}", path);
            string json = TextFileConcatenation.ReadMultipartFile(path);
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

        private void VerifyNavigator(
            string input,
            Exception expectedException,
            bool performExtraChecks = true)
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
                    JsonToken[] tokensFromReader = JsonNavigatorTests.GetTokensWithReader(jsonReader);

                    // Test text
                    IJsonNavigator textNavigator = JsonNavigator.Create(Encoding.UTF8.GetBytes(input));
                    IJsonNavigatorNode textRootNode = textNavigator.GetRootNode();
                    JsonToken[] tokensFromTextNavigator = JsonNavigatorTests.GetTokensFromNode(textRootNode, textNavigator, performExtraChecks);

                    Assert.IsTrue(tokensFromTextNavigator.SequenceEqual(tokensFromReader));

                    // Test binary
                    byte[] binaryInput = JsonTestUtils.ConvertTextToBinary(input);
                    IJsonNavigator binaryNavigator = JsonNavigator.Create(binaryInput);
                    IJsonNavigatorNode binaryRootNode = binaryNavigator.GetRootNode();
                    JsonToken[] tokensFromBinaryNavigator = JsonNavigatorTests.GetTokensFromNode(binaryRootNode, binaryNavigator, performExtraChecks);

                    Assert.IsTrue(tokensFromBinaryNavigator.SequenceEqual(tokensFromReader));

                    // Test binary + user string encoding
                    JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 4096);
                    byte[] binaryWithUserStringEncodingInput = JsonTestUtils.ConvertTextToBinary(input, jsonStringDictionary);
                    if (jsonStringDictionary.TryGetStringAtIndex(index: 0, value: out _))
                    {
                        Assert.IsFalse(binaryWithUserStringEncodingInput.SequenceEqual(binaryInput), "Binary should be different with user string encoding");
                    }

                    IJsonNavigator binaryNavigatorWithUserStringEncoding = JsonNavigator.Create(binaryInput, jsonStringDictionary);
                    IJsonNavigatorNode binaryRootNodeWithUserStringEncoding = binaryNavigatorWithUserStringEncoding.GetRootNode();
                    JsonToken[] tokensFromBinaryNavigatorWithUserStringEncoding = JsonNavigatorTests.GetTokensFromNode(binaryRootNode, binaryNavigator, performExtraChecks);

                    Assert.IsTrue(tokensFromBinaryNavigatorWithUserStringEncoding.SequenceEqual(tokensFromReader));
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        internal static JsonToken[] GetTokensWithReader(IJsonReader jsonReader)
        {
            List<JsonToken> tokens = new List<JsonToken>();
            while (jsonReader.Read())
            {
                JsonToken token;
                switch (jsonReader.CurrentTokenType)
                {
                    case JsonTokenType.NotStarted:
                        throw new InvalidOperationException();

                    case JsonTokenType.BeginArray:
                        token = JsonToken.ArrayStart();
                        break;

                    case JsonTokenType.EndArray:
                        token = JsonToken.ArrayEnd();
                        break;

                    case JsonTokenType.BeginObject:
                        token = JsonToken.ObjectStart();
                        break;

                    case JsonTokenType.EndObject:
                        token = JsonToken.ObjectEnd();
                        break;

                    case JsonTokenType.String:
                        token = JsonToken.String(jsonReader.GetStringValue());
                        break;

                    case JsonTokenType.Number:
                        token = JsonToken.Number(jsonReader.GetNumberValue());
                        break;

                    case JsonTokenType.True:
                        token = JsonToken.Boolean(true);
                        break;

                    case JsonTokenType.False:
                        token = JsonToken.Boolean(false);
                        break;

                    case JsonTokenType.Null:
                        token = JsonToken.Null();
                        break;

                    case JsonTokenType.FieldName:
                        token = JsonToken.FieldName(jsonReader.GetStringValue());
                        break;

                    case JsonTokenType.Int8:
                        token = JsonToken.Int8(jsonReader.GetInt8Value());
                        break;

                    case JsonTokenType.Int16:
                        token = JsonToken.Int16(jsonReader.GetInt16Value());
                        break;

                    case JsonTokenType.Int32:
                        token = JsonToken.Int32(jsonReader.GetInt32Value());
                        break;

                    case JsonTokenType.Int64:
                        token = JsonToken.Int64(jsonReader.GetInt64Value());
                        break;

                    case JsonTokenType.UInt32:
                        token = JsonToken.UInt32(jsonReader.GetUInt32Value());
                        break;

                    case JsonTokenType.Float32:
                        token = JsonToken.Float32(jsonReader.GetFloat32Value());
                        break;

                    case JsonTokenType.Float64:
                        token = JsonToken.Float64(jsonReader.GetFloat64Value());
                        break;

                    case JsonTokenType.Guid:
                        token = JsonToken.Guid(jsonReader.GetGuidValue());
                        break;

                    case JsonTokenType.Binary:
                        token = JsonToken.Binary(jsonReader.GetBinaryValue());
                        break;

                    default:
                        throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {jsonReader.CurrentTokenType}");
                }

                tokens.Add(token);
            }

            return tokens.ToArray();
        }

        internal static JsonToken[] GetTokensFromNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            switch (navigator.GetNodeType(node))
            {
                case JsonNodeType.Null:
                    return new JsonToken[] { JsonToken.Null() };

                case JsonNodeType.False:
                    return new JsonToken[] { JsonToken.Boolean(false) };

                case JsonNodeType.True:
                    return new JsonToken[] { JsonToken.Boolean(true) };

                case JsonNodeType.Number:
                    return new JsonToken[] { JsonToken.Number(navigator.GetNumberValue(node)) };

                case JsonNodeType.String:
                    return new JsonToken[] { JsonToken.String(navigator.GetStringValue(node)) };

                case JsonNodeType.Array:
                    return JsonNavigatorTests.GetTokensFromArrayNode(node, navigator, performCorrectnessCheck);

                case JsonNodeType.Object:
                    return JsonNavigatorTests.GetTokensFromObjectNode(node, navigator, performCorrectnessCheck);

                case JsonNodeType.FieldName:
                    return new JsonToken[] { JsonToken.FieldName(navigator.GetStringValue(node)) };

                case JsonNodeType.Int8:
                    return new JsonToken[] { JsonToken.Int8(navigator.GetInt8Value(node)) };

                case JsonNodeType.Int16:
                    return new JsonToken[] { JsonToken.Int16(navigator.GetInt16Value(node)) };

                case JsonNodeType.Int32:
                    return new JsonToken[] { JsonToken.Int32(navigator.GetInt32Value(node)) };

                case JsonNodeType.Int64:
                    return new JsonToken[] { JsonToken.Int64(navigator.GetInt64Value(node)) };

                case JsonNodeType.UInt32:
                    return new JsonToken[] { JsonToken.UInt32(navigator.GetUInt32Value(node)) };

                case JsonNodeType.Float32:
                    return new JsonToken[] { JsonToken.Float32(navigator.GetFloat32Value(node)) };

                case JsonNodeType.Float64:
                    return new JsonToken[] { JsonToken.Float64(navigator.GetFloat64Value(node)) };

                case JsonNodeType.Guid:
                    return new JsonToken[] { JsonToken.Guid(navigator.GetGuidValue(node)) };

                case JsonNodeType.Binary:
                    return new JsonToken[] { JsonToken.Binary(navigator.GetBinaryValue(node)) };

                default:
                    throw new InvalidOperationException();
            }
        }

        internal static JsonToken[] GetTokensFromObjectNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            // Get the tokens through .GetObjectProperties
            List<JsonToken> tokensFromGetProperties = new List<JsonToken>();
            IEnumerable<ObjectProperty> properties = navigator.GetObjectProperties(node);

            tokensFromGetProperties.Add(JsonToken.ObjectStart());
            foreach (ObjectProperty property in properties)
            {
                string fieldname = navigator.GetStringValue(property.NameNode);
                tokensFromGetProperties.Add(JsonToken.FieldName(fieldname));
                tokensFromGetProperties.AddRange(JsonNavigatorTests.GetTokensFromNode(property.ValueNode, navigator, performCorrectnessCheck));
            }
            tokensFromGetProperties.Add(JsonToken.ObjectEnd());

            if (performCorrectnessCheck)
            {
                // Get the tokens again through .TryGetObjectProperty
                List<JsonToken> tokensFromTryGetProperty = new List<JsonToken>();

                tokensFromTryGetProperty.Add(JsonToken.ObjectStart());
                foreach (ObjectProperty objectProperty in properties)
                {
                    ObjectProperty propertyFromTryGetProperty;
                    string fieldname = navigator.GetStringValue(objectProperty.NameNode);
                    if (navigator.TryGetObjectProperty(node, fieldname, out propertyFromTryGetProperty))
                    {
                        tokensFromTryGetProperty.Add(JsonToken.FieldName(fieldname));
                        tokensFromTryGetProperty.AddRange(JsonNavigatorTests.GetTokensFromNode(propertyFromTryGetProperty.ValueNode, navigator, performCorrectnessCheck));
                    }
                    else
                    {
                        Assert.Fail($"Failed to get object property with name: {fieldname}");
                    }
                }
                tokensFromTryGetProperty.Add(JsonToken.ObjectEnd());
                Assert.AreEqual(properties.Count(), navigator.GetObjectPropertyCount(node));
                Assert.IsTrue(tokensFromGetProperties.SequenceEqual(tokensFromTryGetProperty));
            }

            return tokensFromGetProperties.ToArray();
        }

        internal static JsonToken[] GetTokensFromArrayNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            // Get tokens once through IEnumerable
            List<JsonToken> tokensFromIEnumerable = new List<JsonToken>();
            IEnumerable<IJsonNavigatorNode> arrayItems = navigator.GetArrayItems(node);

            tokensFromIEnumerable.Add(JsonToken.ArrayStart());
            foreach (IJsonNavigatorNode arrayItem in arrayItems)
            {
                tokensFromIEnumerable.AddRange(JsonNavigatorTests.GetTokensFromNode(arrayItem, navigator, performCorrectnessCheck));
            }

            tokensFromIEnumerable.Add(JsonToken.ArrayEnd());

            if (performCorrectnessCheck)
            {
                // Get tokens once again through indexer
                List<JsonToken> tokensFromIndexer = new List<JsonToken>();
                tokensFromIndexer.Add(JsonToken.ArrayStart());
                for (int i = 0; i < navigator.GetArrayItemCount(node); ++i)
                {
                    tokensFromIndexer.AddRange(JsonNavigatorTests.GetTokensFromNode(navigator.GetArrayItemAt(node, i), navigator, performCorrectnessCheck));
                }

                tokensFromIndexer.Add(JsonToken.ArrayEnd());

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