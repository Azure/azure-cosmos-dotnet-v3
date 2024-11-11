//-----------------------------------------------------------------------
// <copyright file="JsonRoundTripsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections;
    using System.Diagnostics;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.Tests.Json.JsonTestUtils;

    [TestClass]
    public class JsonRoundTripsTests
    {
        #region Literals
        [TestMethod]
        [Owner("mayapainter")]
        public void TrueTest()
        {
            string input = "true";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void FalseTest()
        {
            string input = "false";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullTest()
        {
            string input = "null";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }
        #endregion

        #region Numbers
        [TestMethod]
        [Owner("mayapainter")]
        public void IntegerTest()
        {
            string input = "1337";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void DoubleTest()
        {
            string input = "1337.7";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NegativeNumberTest()
        {
            string input = "-1337.7";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberWithScientificNotationTest()
        {
            string input = "6.02252E+23";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ScientificWithPostitiveExponent()
        {
            string input = "6.02252E+23";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ScientificWithNegativeExponent()
        {
            string input = "6.02252E-23";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }
        #endregion

        #region Strings
        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyStringTest()
        {
            string input = "\"\"";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StringTest()
        {
            string input = "\"Hello World\"";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }
        #endregion

        #region Arrays
        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformNumberArrayTest1()
        {
            // Int8
            string input = "[ -2, -1, 0, 1, 2]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformNumberArrayTest2()
        {
            // UInt8
            string input = "[ 15, 0, 4, 25, 100]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformNumberArrayTest3()
        {
            // Int16
            string input = "[ 300, -1251, 8944, -1024 ]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformNumberArrayTest4()
        {
            // Int32
            string input = "[ 77111, 187345, -1, 0, 255 ]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformNumberArrayTest5()
        {
            // Int64
            string input = "[ -128, 8589934592, -8, 4, 127 ]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformNumberArrayTest6()
        {
            // Float64
            string input = "[ -1.1, 1.1, 0, 4, 1.5 ]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformArrayOfNumberArrayTest1()
        {
            // Int8
            string input = "[ [1, -2], [-1, 2], [-1, -2] ]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformArrayOfNumberArrayTest2()
        {
            // UInt8
            string input = "[ [40, 50, 60], [50, 60, 70], [60, 70, 80] ]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformArrayOfNumberArrayTest3()
        {
            // Int16
            string input = "[ [400, 500, 600, 700], [-400, -500, -600, -700] ]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformArrayOfNumberArrayTest4()
        {
            // Int32
            string input = "[ [222000, 333000, 444000, 555000], [222000, 333000, 444000, 555000] ]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformArrayOfNumberArrayTest5()
        {
            // Int64
            string input = "[ [222000222000, 333000333000, 444000444000, 555000555000], [222000222000, 333000333000, 444000444000, 555000555000] ]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UniformArrayOfNumberArrayTest6()
        {
            // Float64
            string input = "[ [2.1, 1.1, 0.1, -0.1, -1.1, -2.1], [1.1, 1.2, 1.3, 1.4, 1.5, 1.6], [0.1, 0.2, 0.3, 0.4, 0.5, 0.6] ]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.1, -1, -1.1, 1, 2, \"hello\", null, true, false]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }
        #endregion

        #region Escaping
        [TestMethod]
        [Owner("mayapainter")]
        public void EscapeCharacterTest()
        {
            /// <summary>
            /// Set of all escape characters in JSON.
            /// </summary>
            Tuple<string, string>[] escapeCharacters = new Tuple<string, string>[]
            {
                new Tuple<string, string>(@"\b", "\b"),
                new Tuple<string, string>(@"\f", "\f"),
                new Tuple<string, string>(@"\n", "\n"),
                new Tuple<string, string>(@"\r", "\r"),
                new Tuple<string, string>(@"\t", "\t"),
                new Tuple<string, string>(@"\""", "\""),
                new Tuple<string, string>(@"\\", @"\"),
                new Tuple<string, string>(@"\/", "/"),
            };

            foreach (Tuple<string, string> escapeCharacter in escapeCharacters)
            {
                string input = "\"" + escapeCharacter.Item1 + "\"";
                JsonRoundTripsTests.VerifyRoundTripTest(input);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void WhitespaceCharacterTest()
        {
            string input = "[" + " " + "\"hello\"" + "," + "\t" + "\"my\"" + "\r" + "," + "\"name\"" + "\n" + "," + "\"is\"" + "]";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UnicodeTest()
        {
            // the user might literally paste a unicode character into the json.
            string unicodeString = "\"€\"";
            JsonRoundTripsTests.VerifyRoundTripTest(unicodeString);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EmojiUTF32Test()
        {
            // the user might literally paste a utf 32 character (like the poop emoji).
            string unicodeString = "\"💩\"";

            JsonRoundTripsTests.VerifyRoundTripTest(unicodeString);
        }
        #endregion

        #region Objects
        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyObjectTest()
        {
            string input = "{}";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";
            JsonRoundTripsTests.VerifyRoundTripTest(input);
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
            JsonRoundTripsTests.VerifyRoundTripTest(input);
        }
        #endregion

        #region Limits
        [TestMethod]
        [Owner("mayapainter")]
        public void NumberLimitsTest()
        {
            // min byte
            string minByteInput = "0";
            JsonRoundTripsTests.VerifyRoundTripTest(minByteInput);

            // max byte
            string maxByteInput = "255";
            JsonRoundTripsTests.VerifyRoundTripTest(maxByteInput);

            // min short
            string minShortInput = "-32768";
            JsonRoundTripsTests.VerifyRoundTripTest(minShortInput);

            // max short
            string maxShortInput = "32767";
            JsonRoundTripsTests.VerifyRoundTripTest(maxShortInput);

            // min int
            string minIntInput = "-2147483648";
            JsonRoundTripsTests.VerifyRoundTripTest(minIntInput);

            // max int
            string maxIntInput = "2147483647";
            JsonRoundTripsTests.VerifyRoundTripTest(maxIntInput);

            // min long
            string minLongInput = "-9223372036854775808";
            JsonRoundTripsTests.VerifyRoundTripTest(minLongInput);

            // max long
            string maxLongInput = "9223372036854775807";
            JsonRoundTripsTests.VerifyRoundTripTest(maxLongInput);

            // min double
            string minDoubleInput = "-1.7976931348623157E+308";
            JsonRoundTripsTests.VerifyRoundTripTest(minDoubleInput);

            // max double
            string maxDoubleInput = "1.7976931348623157E+308";
            JsonRoundTripsTests.VerifyRoundTripTest(maxDoubleInput);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ArrayLengthLimitsTest()
        {
            // empty array 
            string emptyArrayInput = "[]";
            JsonRoundTripsTests.VerifyRoundTripTest(emptyArrayInput);

            // single item array 
            string singleItemArrayInput = @"[""a""]";
            JsonRoundTripsTests.VerifyRoundTripTest(singleItemArrayInput);

            // max 1 byte length array
            string maxByteLengthPayload = new string('a', byte.MaxValue - 1 - 1);
            string maxByteLengthInput = @"[""" + maxByteLengthPayload + @"""]";
            JsonRoundTripsTests.VerifyRoundTripTest(maxByteLengthInput);

            // max 2 byte length array
            string maxUShortLengthPayload = new string('a', ushort.MaxValue - 1 - 2);
            string maxUShortLengthInput = @"[""" + maxUShortLengthPayload + @"""]";
            JsonRoundTripsTests.VerifyRoundTripTest(maxUShortLengthInput);

            // max 4 byte length array
            string maxUIntLengthPayload = new string('a', ushort.MaxValue);
            string maxUIntLengthInput = @"[""" + maxUIntLengthPayload + @"""]";
            JsonRoundTripsTests.VerifyRoundTripTest(maxUIntLengthInput);
        }
        #endregion

        #region CuratedDocuments
        [TestMethod]
        [Owner("mayapainter")]
        public void CombinedScriptsDataTest()
        {
            this.VerifyCuratedJsonRoundTripTest("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        [Ignore] // Takes too long
        public void CountriesTest()
        {
            this.VerifyCuratedJsonRoundTripTest("countries");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void DevTestCollTest()
        {
            this.VerifyCuratedJsonRoundTripTest("devtestcoll.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void LastFMTest()
        {
            this.VerifyCuratedJsonRoundTripTest("lastfm");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void LogDataTest()
        {
            this.VerifyCuratedJsonRoundTripTest("LogData.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MillionSong1KDocumentsTest()
        {
            this.VerifyCuratedJsonRoundTripTest("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MsnCollectionTest()
        {
            this.VerifyCuratedJsonRoundTripTest("MsnCollection.json");
        }

        [TestMethod]
        [Owner("ndeshpan")]
        public void LargeArrayBinaryJsonTest()
        {
            StringBuilder builder = new StringBuilder((1 << 24) + 50);
            builder.Append('[');
            for (int x = 1 << 24; x < (1 << 24) + 3355450; ++x)
            {
                builder.Append(x);
                builder.Append(',');
            }
            builder.Append("\"string_one\"");
            builder.Append(',');
            builder.Append("\"string_two\"");
            builder.Append(',');
            builder.Append("\"string_two\"");
            builder.Append(']');

            string json = builder.ToString();
            byte[] binaryJson = JsonTestUtils.ConvertTextToBinary(json);
            IJsonNavigator navigator = JsonNavigator.Create(binaryJson);
            int count = navigator.GetArrayItemCount(navigator.GetRootNode());
            IJsonNavigatorNode node = navigator.GetArrayItemAt(navigator.GetRootNode(), count - 1);
            string stringValue = navigator.GetStringValue(node);
            Assert.AreEqual("string_two", stringValue);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NutritionDataTest()
        {
            this.VerifyCuratedJsonRoundTripTest("NutritionData");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void RunsCollectionTest()
        {
            this.VerifyCuratedJsonRoundTripTest("runsCollection");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StatesCommitteesTest()
        {
            this.VerifyCuratedJsonRoundTripTest("states_committees.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StatesLegislatorsTest()
        {
            this.VerifyCuratedJsonRoundTripTest("states_legislators");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Store01Test()
        {
            this.VerifyCuratedJsonRoundTripTest("store01C.json");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TicinoErrorBucketsTest()
        {
            this.VerifyCuratedJsonRoundTripTest("TicinoErrorBuckets");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TwitterDataTest()
        {
            this.VerifyCuratedJsonRoundTripTest("twitter_data");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Ups1Test()
        {
            this.VerifyCuratedJsonRoundTripTest("ups1");
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void XpertEventsTest()
        {
            this.VerifyCuratedJsonRoundTripTest("XpertEvents");
        }
        #endregion

        private static void VerifyRoundTripTest(string inputJson)
        {
            // Do the actual roundtrips
            JsonToken[] inputTokens = JsonTestUtils.ReadJsonDocument(inputJson);
            JsonRoundTripsTests.MultiSerializationRoundTrip(inputTokens, inputJson);
        }

        // Checks to see if we can go from a JsonReader to a NewtonsoftWriter and get back the original document and visa versa
        private void VerifyCuratedJsonRoundTripTest(string filename, int maxNumberOfItems = 100)
        {
            string inputJson = JsonTestUtils.LoadJsonCuratedDocument(filename);
            inputJson = JsonTestUtils.RandomSampleJson(inputJson, maxNumberOfItems, seed: 42);
            JsonToken[] inputTokens = JsonTestUtils.ReadJsonDocument(inputJson);
            MultiSerializationRoundTrip(inputTokens, inputJson);
        }

        private static void MultiSerializationRoundTrip(JsonToken[] inputTokens, string inputJson)
        {
            {
                // Verify native Cosmos formats and write options round-trips
                JsonTestUtils.SerializationSpec[] serializationSpecs =
                {
                    SerializationSpec.Text(JsonWriteOptions.None),
                    SerializationSpec.Binary(JsonWriteOptions.None),
                    SerializationSpec.Binary(JsonWriteOptions.EnableNumberArrays),
                };

                RewriteScenario[] rewriteScenarios =
                {
                    RewriteScenario.NavigatorRoot,
                    RewriteScenario.NavigatorNode,
                    RewriteScenario.ReaderAll,
                    RewriteScenario.ReaderToken,
                };

                MultiSerializationRoundTrip(inputTokens, inputJson, serializationSpecs, rewriteScenarios);
            }

            {
                // Verify Text to Newtonsoft round-trip
                SerializationSpec[] serializationSpecs =
                {
                    SerializationSpec.Text(JsonWriteOptions.None),
                    SerializationSpec.Newtonsoft(),
                };

                RewriteScenario[] rewriteScenarios =
                {
                    RewriteScenario.NavigatorNode,
                    RewriteScenario.ReaderToken,
                };

                MultiSerializationRoundTrip(inputTokens, inputJson, serializationSpecs, rewriteScenarios);
            }
        }

        private static void MultiSerializationRoundTrip(
            JsonToken[] inputTokens,
            string inputJson,
            SerializationSpec[] serializationSpecs,
            RewriteScenario[] rewriteScenarios)
        {
            Console.WriteLine();
            Console.WriteLine($"Input JSON Length: {inputJson.Length}");
            Console.WriteLine($"Input Token Count: {inputTokens.Length}");

            ReadOnlyMemory<byte>[] expectedOutputResults = new ReadOnlyMemory<byte>[serializationSpecs.Length];

            foreach (SerializationSpec inputSpec in serializationSpecs)
            {
                Stopwatch timer = Stopwatch.StartNew();

                IJsonWriter inputWriter = inputSpec.IsNewtonsoft ?
                    NewtonsoftToCosmosDBWriter.CreateTextWriter() :
                    JsonWriter.Create(inputSpec.SerializationFormat, inputSpec.WriteOptions);

                JsonTestUtils.WriteTokens(inputTokens, inputWriter, writeAsUtf8String: true);
                ReadOnlyMemory<byte> inputResult = inputWriter.GetResult();

                timer.Stop();

                Console.WriteLine();
                Console.WriteLine($"  -- Input Format '{inputSpec.SerializationFormatToString()}'");
                Console.WriteLine($"    Input Write Time (ms): {timer.ElapsedMilliseconds}");
                Console.WriteLine($"    Input Result Length  : {inputResult.Length}");

                for (int i = 0; i < serializationSpecs.Length; i++)
                {
                    SerializationSpec outputSpec = serializationSpecs[i];

                    Console.WriteLine($"    -- Output Format '{outputSpec.SerializationFormatToString()}'");

                    RoundTripResult roundTripResult = null;
                    foreach (RewriteScenario scenario in rewriteScenarios)
                    {
                        roundTripResult = VerifyJsonRoundTrip(
                            inputResult,
                            inputJson,
                            inputSpec,
                            outputSpec,
                            scenario,
                            expectedOutputResults[i],
                            (string _) => new JsonNewtonsoftNavigator(_));

                        expectedOutputResults[i] = roundTripResult.OutputResult;

                        Console.WriteLine($"      Scenario '{scenario}'");
                        Console.WriteLine($"        Execution Time    (ms): {roundTripResult.ExecutionTime,5}");
                        Console.WriteLine($"        Verification Time (ms): {roundTripResult.VerificationTime,5}");
                    }
                }
            }
        }
    }
}