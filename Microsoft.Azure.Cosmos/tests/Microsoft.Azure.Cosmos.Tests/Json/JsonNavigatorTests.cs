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
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;

    [TestClass]
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
        [Owner("mayapainter")]
        public void TrueTest()
        {
            string input = "true";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void FalseTest()
        {
            string input = "false";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullTest()
        {
            string input = "null";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void IntegerTest()
        {
            string input = "1337";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void DoubleTest()
        {
            string input = "1337.0";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NegativeNumberTest()
        {
            string input = "-1337.0";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberWithScientificNotationTest()
        {
            string input = "6.02252e23";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberRegressionTest()
        {
            // regression test - the value 0.00085647800000000004 was being incorrectly rejected
            string numberValueString = "0.00085647800000000004";

            JsonNavigatorTests.VerifyNavigator(numberValueString);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StringTest()
        {
            string input = "\"Hello World\"";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1, -7.3e-2, 77.0001e90 ]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.0, -1, -1.0, 1, 2, \"hello\", null, true, false]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StrangeNumberArrayTest()
        {
            string input = @"[
                1111111110111111111011111111101111111110,
                1111111110111111111011111111101111111110111111111011111111101111111110,
                11111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110,
                1111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110
            ]";

            try
            {
                JsonNavigatorTests.VerifyNavigator(input);
            }
            catch (MaterializationFailedToMatchException)
            {
                // Newtonsoft does not use IEEE double precision for these long integers.
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyObjectTest()
        {
            string input = "{}";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
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

            JsonNavigatorTests.VerifyNavigator(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int8Test()
        {
            sbyte[] values = new sbyte[] { sbyte.MinValue, sbyte.MinValue + 1, -1, 0, 1, sbyte.MaxValue, sbyte.MaxValue - 1 };
            foreach (sbyte value in values)
            {
                string input = $"I{value}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int16Test()
        {
            short[] values = new short[] { short.MinValue, short.MinValue + 1, -1, 0, 1, short.MaxValue, short.MaxValue - 1 };
            foreach (short value in values)
            {
                string input = $"H{value}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int32Test()
        {
            int[] values = new int[] { int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue, int.MaxValue - 1 };
            foreach (int value in values)
            {
                string input = $"L{value}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int64Test()
        {
            long[] values = new long[] { long.MinValue, long.MinValue + 1, -1, 0, 1, long.MaxValue, long.MaxValue - 1 };
            foreach (long value in values)
            {
                string input = $"LL{value}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UInt32Test()
        {
            uint[] values = new uint[] { uint.MinValue, uint.MinValue + 1, 0, 1, uint.MaxValue, uint.MaxValue - 1 };
            foreach (uint value in values)
            {
                string input = $"UL{value}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Float32Test()
        {
            float[] values = new float[] { float.MinValue, float.MinValue + 1, 0, 1, float.MaxValue, float.MaxValue - 1 };
            foreach (float value in values)
            {
                string input = $"S{value.ToString("G9", CultureInfo.InvariantCulture)}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Float64Test()
        {
            double[] values = new double[] { double.MinValue, double.MinValue + 1, 0, 1, double.MaxValue, double.MaxValue - 1 };
            foreach (double value in values)
            {
                string input = $"D{value.ToString("G17", CultureInfo.InvariantCulture)}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void GuidTest()
        {
            Guid[] values = new Guid[] { Guid.Empty, Guid.NewGuid() };
            foreach (Guid value in values)
            {
                string input = $"G{value.ToString()}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void BinaryTest()
        {
            {
                // Empty Binary
                string input = $"B";
                JsonNavigatorTests.VerifyNavigator(input);
            }

            {
                // Binary 1 Byte Length
                IReadOnlyList<byte> binary = Enumerable.Range(0, 25).Select(x => (byte)x).ToList();
                string input = $"B{Convert.ToBase64String(binary.ToArray())}";
                JsonNavigatorTests.VerifyNavigator(input);
            }
        }
        #endregion

        #region CuratedDocs
        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentCombinedScriptsDataTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        [Ignore] // This test takes too long
        public void CuratedDocumentCountriesTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("countries", false);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentDevTestCollTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("devtestcoll.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentLastFMTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("lastfm");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentLogDataTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("LogData.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentMillionSong1KDocumentsTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentMsnCollectionTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("MsnCollection.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentNutritionDataTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("NutritionData");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentRunsCollectionTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("runsCollection");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentStatesCommitteesTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("states_committees.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentStatesLegislatorsTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("states_legislators");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentStore01Test()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("store01C.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentTicinoErrorBucketsTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("TicinoErrorBuckets");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentTwitterDataTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("twitter_data");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentUps1Test()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("ups1");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void CuratedDocumentXpertEventsTest()
        {
            JsonNavigatorTests.VerifyNavigatorWithCuratedDoc("XpertEvents");
        }

        private static void VerifyNavigatorWithCuratedDoc(string filename, bool performExtraChecks = true)
        {
            string json = JsonTestUtils.LoadJsonCuratedDocument(filename);
#if true
            json = JsonTestUtils.RandomSampleJson(json, maxNumberOfItems: 10);
#endif

            JsonNavigatorTests.VerifyNavigator(json, performExtraChecks);
        }
        #endregion

        private static void VerifyNavigator(
            string input,
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

                    JsonToken[] tokensFromReader = JsonTestUtils.ReadJsonDocument(input);

                    // Text
                    IJsonNavigator textNavigator = JsonNavigator.Create(Encoding.UTF8.GetBytes(input));

                    // Binary
                    byte[] binaryInput = JsonTestUtils.ConvertTextToBinary(input);
                    IJsonNavigator binaryNavigator = JsonNavigator.Create(binaryInput);

                    // Test
                    foreach (IJsonNavigator jsonNavigator in new IJsonNavigator[] { textNavigator, binaryNavigator })
                    {
                        IJsonNavigatorNode rootNode = jsonNavigator.GetRootNode();
                        JsonToken[] tokensFromNavigator = JsonNavigatorTests.GetTokensFromNode(rootNode, jsonNavigator, performExtraChecks);
                        Assert.AreEqual(tokensFromNavigator.Length, tokensFromReader.Length);
                        IEnumerable<(JsonToken, JsonToken)> zippedTokens = tokensFromNavigator.Zip(tokensFromReader, (first, second) => (first, second));
                        foreach ((JsonToken tokenFromNavigator, JsonToken tokenFromReader) in zippedTokens)
                        {
                            if (!tokenFromNavigator.Equals(tokenFromReader))
                            {
                                Assert.Fail();
                            }
                        }

                        // Test materialize
                        JToken materializedToken = CosmosElement.Dispatch(jsonNavigator, rootNode).Materialize<JToken>();

                        try
                        {
                            if (materializedToken.ToString() != JToken.Parse(input).ToString())
                            {
                                throw new MaterializationFailedToMatchException();
                            }
                        }
                        catch (Newtonsoft.Json.JsonReaderException)
                        {
                            // If the input is extended type we ignore this check.
                        }
                    }
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        private static JsonToken[] GetTokensFromNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
        {
            JsonNodeType nodeType = navigator.GetNodeType(node);
            switch (nodeType)
            {
                case JsonNodeType.Null:
                    return new JsonToken[] { JsonToken.Null() };

                case JsonNodeType.False:
                    return new JsonToken[] { JsonToken.Boolean(false) };

                case JsonNodeType.True:
                    return new JsonToken[] { JsonToken.Boolean(true) };

                case JsonNodeType.Number64:
                    return new JsonToken[] { JsonToken.Number(navigator.GetNumber64Value(node)) };

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

        private static JsonToken[] GetTokensFromObjectNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
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
                    string fieldname = navigator.GetStringValue(objectProperty.NameNode);
                    if (navigator.TryGetObjectProperty(node, fieldname, out ObjectProperty propertyFromTryGetProperty))
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

        private static JsonToken[] GetTokensFromArrayNode(IJsonNavigatorNode node, IJsonNavigator navigator, bool performCorrectnessCheck)
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

                int itemCount = navigator.GetArrayItemCount(node);
                for (int i = 0; i < itemCount; ++i)
                {
                    tokensFromIndexer.AddRange(JsonNavigatorTests.GetTokensFromNode(navigator.GetArrayItemAt(node, i), navigator, performCorrectnessCheck));
                }

                tokensFromIndexer.Add(JsonToken.ArrayEnd());

                Assert.AreEqual(arrayItems.Count(), navigator.GetArrayItemCount(node));
                Assert.IsTrue(tokensFromIEnumerable.SequenceEqual(tokensFromIndexer));
            }

            return tokensFromIEnumerable.ToArray();
        }

        private sealed class MaterializationFailedToMatchException : Exception
        {
            public MaterializationFailedToMatchException()
            {
            }
        }
    }
}