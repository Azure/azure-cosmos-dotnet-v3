//-----------------------------------------------------------------------
// <copyright file="JsonRoundTripsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonRoundTripsTests
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

        #region Literals
        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            string input = "true";
            JsonToken[] token =
            {
                JsonToken.Boolean(true)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";
            JsonToken[] token =
            {
                JsonToken.Boolean(false)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";
            JsonToken[] token =
            {
                JsonToken.Null()
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }
        #endregion
        #region Numbers
        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string input = "1337";

            JsonToken[] token =
            {
                JsonToken.Number(1337)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.7";

            JsonToken[] token =
            {
                JsonToken.Number(1337.7)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string input = "-1337.7";

            JsonToken[] token =
            {
                JsonToken.Number(-1337.7)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string input2 = "6.02252E+23";

            JsonToken[] token =
            {
                JsonToken.Number(6.02252e23)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input2, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithPostitiveExponent()
        {
            string input2 = "6.02252E+23";

            JsonToken[] token =
            {
                JsonToken.Number(6.02252e+23)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input2, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithNegativeExponent()
        {
            string input2 = "6.02252E-23";

            JsonToken[] token =
            {
                JsonToken.Number(6.02252e-23)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input2, token);
        }
        #endregion
        #region Strings
        [TestMethod]
        [Owner("brchon")]
        public void EmptyStringTest()
        {
            string input = "\"\"";
            JsonToken[] token =
            {
                JsonToken.String(string.Empty)
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string input = "\"Hello World\"";
            JsonToken[] token =
            {
                JsonToken.String("Hello World")
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }
        #endregion
        #region Arrays
        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(-2),
                JsonToken.Number(-1),
                JsonToken.Number(0),
                JsonToken.Number(1),
                JsonToken.Number(2),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(15),
                JsonToken.Number(22),
                JsonToken.Number(0.1),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.Null(),
                JsonToken.Null(),
                JsonToken.Null(),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.1, -1, -1.1, 1, 2, \"hello\", null, true, false]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(0),
                JsonToken.Number(0.1),
                JsonToken.Number(-1),
                JsonToken.Number(-1.1),
                JsonToken.Number(1),
                JsonToken.Number(2),
                JsonToken.String("hello"),
                JsonToken.Null(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }
        #endregion
        #region Escaping
        [TestMethod]
        [Owner("brchon")]
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
                JsonToken[] token =
                {
                     JsonToken.String(escapeCharacter.Item2),
                };

                JsonRoundTripsTests.PerformRoundTripTest(input, token);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void WhitespaceCharacterTest()
        {
            /// <summary>
            /// http://www.ietf.org/rfc/rfc4627.txt for JSON whitespace definition (Section 2).
            /// </summary>
            char[] whitespaceCharacters = new char[]
            {
                ' ',
                '\t',
                '\r',
                '\n'
            };

            string input = "[" + " " + "\"hello\"" + "," + "\t" + "\"my\"" + "\r" + "," + "\"name\"" + "\n" + "," + "\"is\"" + "]";

            JsonToken[] token =
            {
                JsonToken.ArrayStart(),
                JsonToken.String("hello"),
                JsonToken.String("my"),
                JsonToken.String("name"),
                JsonToken.String("is"),
                JsonToken.ArrayEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnicodeTest()
        {
            // the user might literally paste a unicode character into the json.
            string unicodeString = "\"€\"";
            // This is the 2 byte equivalent.
            string expectedString = "€";

            JsonToken[] token =
            {
                 JsonToken.String(expectedString),
            };

            JsonRoundTripsTests.PerformRoundTripTest(unicodeString, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmojiUTF32Test()
        {
            // the user might literally paste a utf 32 character (like the poop emoji).
            string unicodeString = "\"💩\"";
            // This is the 4 byte equivalent.
            string expectedString = "💩";

            JsonToken[] token =
            {
                 JsonToken.String(expectedString),
            };

            JsonRoundTripsTests.PerformRoundTripTest(unicodeString, token);
        }
        #endregion
        #region Objects
        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            string input = "{}";

            JsonToken[] token =
            {
                 JsonToken.ObjectStart(),
                 JsonToken.ObjectEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            JsonToken[] token =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("GlossDiv"),
                JsonToken.Number(10),
                JsonToken.FieldName("title"),
                JsonToken.String("example glossary"),
                JsonToken.ObjectEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
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

            JsonToken[] token =
            {
                JsonToken.ObjectStart(),

                    JsonToken.FieldName("id"),
                    JsonToken.String("7029d079-4016-4436-b7da-36c0bae54ff6"),

                    JsonToken.FieldName("double"),
                    JsonToken.Number(0.18963001816981939),

                    JsonToken.FieldName("int"),
                    JsonToken.Number(-1330192615),

                    JsonToken.FieldName("string"),
                    JsonToken.String("XCPCFXPHHF"),

                    JsonToken.FieldName("boolean"),
                    JsonToken.Boolean(true),

                    JsonToken.FieldName("null"),
                    JsonToken.Null(),

                    JsonToken.FieldName("datetime"),
                    JsonToken.String("2526-07-11T18:18:16.4520716"),

                    JsonToken.FieldName("spatialPoint"),
                    JsonToken.ObjectStart(),
                        JsonToken.FieldName("type"),
                        JsonToken.String("Point"),

                        JsonToken.FieldName("coordinates"),
                        JsonToken.ArrayStart(),
                            JsonToken.Number(118.9897),
                            JsonToken.Number(-46.6781),
                        JsonToken.ArrayEnd(),
                    JsonToken.ObjectEnd(),

                    JsonToken.FieldName("text"),
                    JsonToken.String("tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby"),
                JsonToken.ObjectEnd(),
            };

            JsonRoundTripsTests.PerformRoundTripTest(input, token);
        }
        #endregion
        #region Limits
        [TestMethod]
        [Owner("brchon")]
        public void NumberLimitsTest()
        {
            // min byte
            string minByteInput = "0";
            JsonToken[] minByteTokens =
            {
                JsonToken.Number(byte.MinValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(minByteInput, minByteTokens);

            // max byte
            string maxByteInput = "255";
            JsonToken[] maxByteTokens =
            {
                JsonToken.Number(byte.MaxValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxByteInput, maxByteTokens);

            // min short
            string minShortInput = "-32768";
            JsonToken[] minShortTokens =
            {
                JsonToken.Number(short.MinValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(minShortInput, minShortTokens);

            // max short
            string maxShortInput = "32767";
            JsonToken[] maxShortTokens =
            {
                JsonToken.Number(short.MaxValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxShortInput, maxShortTokens);

            // min int
            string minIntInput = "-2147483648";
            JsonToken[] minIntTokens =
            {
                JsonToken.Number(int.MinValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(minIntInput, minIntTokens);

            // max int
            string maxIntInput = "2147483647";
            JsonToken[] maxIntTokens =
            {
                JsonToken.Number(int.MaxValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxIntInput, maxIntTokens);

            // min long
            string minLongInput = "-9223372036854775808";
            JsonToken[] minLongTokens =
            {
                JsonToken.Number(long.MinValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(minLongInput, minLongTokens);

            // max long
            string maxLongInput = "9223372036854775807";
            JsonToken[] maxLongTokens =
            {
                JsonToken.Number(long.MaxValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxLongInput, maxLongTokens);

            // min double
            string minDoubleInput = "-1.7976931348623157E+308";
            JsonToken[] minDoubleTokens =
            {
                JsonToken.Number(double.MinValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(minDoubleInput, minDoubleTokens);

            // max double
            string maxDoubleInput = "1.7976931348623157E+308";
            JsonToken[] maxDoubleTokens =
            {
                JsonToken.Number(double.MaxValue)
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxDoubleInput, maxDoubleTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayLengthLimitsTest()
        {
            // empty array 
            string emptyArrayInput = "[]";
            JsonToken[] emptyArrayTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd()
            };

            JsonRoundTripsTests.PerformRoundTripTest(emptyArrayInput, emptyArrayTokens);

            // single item array 
            string singleItemArrayInput = @"[""a""]";
            JsonToken[] singleItemArrayTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String("a"),
                JsonToken.ArrayEnd()
            };

            JsonRoundTripsTests.PerformRoundTripTest(singleItemArrayInput, singleItemArrayTokens);

            // max 1 byte length array
            string maxByteLengthPayload = new string('a', byte.MaxValue - 1 - 1);
            string maxByteLengthInput = @"[""" + maxByteLengthPayload + @"""]";
            JsonToken[] maxByteLengthTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String(maxByteLengthPayload),
                JsonToken.ArrayEnd()
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxByteLengthInput, maxByteLengthTokens);

            // max 2 byte length array
            string maxUShortLengthPayload = new string('a', ushort.MaxValue - 1 - 2);
            string maxUShortLengthInput = @"[""" + maxUShortLengthPayload + @"""]";
            JsonToken[] maxUShortLengthTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String(maxUShortLengthPayload),
                JsonToken.ArrayEnd()
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxUShortLengthInput, maxUShortLengthTokens);

            // max 4 byte length array
            string maxUIntLengthPayload = new string('a', ushort.MaxValue);
            string maxUIntLengthInput = @"[""" + maxUIntLengthPayload + @"""]";
            JsonToken[] maxUIntLengthTokens =
            {
                JsonToken.ArrayStart(),
                // 2 of them just to go past int.MaxValue but < uint.MaxValue
                JsonToken.String(maxUIntLengthPayload),
                JsonToken.ArrayEnd()
            };

            JsonRoundTripsTests.PerformRoundTripTest(maxUIntLengthInput, maxUIntLengthTokens);
        }
        #endregion
        #region CuratedDocuments

        [TestMethod]
        [Owner("brchon")]
        public void CombinedScriptsDataTest()
        {
            this.RoundTripTestCuratedJson("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void CountriesTest()
        {
            this.RoundTripTestCuratedJson("countries");
        }

        [TestMethod]
        [Owner("brchon")]
        public void DevTestCollTest()
        {
            this.RoundTripTestCuratedJson("devtestcoll.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LastFMTest()
        {
            this.RoundTripTestCuratedJson("lastfm");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LogDataTest()
        {
            this.RoundTripTestCuratedJson("LogData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MillionSong1KDocumentsTest()
        {
            this.RoundTripTestCuratedJson("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MsnCollectionTest()
        {
            this.RoundTripTestCuratedJson("MsnCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void NutritionDataTest()
        {
            this.RoundTripTestCuratedJson("NutritionData");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            this.RoundTripTestCuratedJson("runsCollection");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesCommitteesTest()
        {
            this.RoundTripTestCuratedJson("states_committees.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesLegislatorsTest()
        {
            this.RoundTripTestCuratedJson("states_legislators");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Store01Test()
        {
            this.RoundTripTestCuratedJson("store01C.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TicinoErrorBucketsTest()
        {
            this.RoundTripTestCuratedJson("TicinoErrorBuckets");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            this.RoundTripTestCuratedJson("twitter_data");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            this.RoundTripTestCuratedJson("ups1");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            this.RoundTripTestCuratedJson("XpertEvents");
        }

        // Checks to see if we can go from a JsonReader to a NewtonsoftWriter and get back the original document and visa versa
        private void RoundTripTestCuratedJson(string path)
        {
            path = string.Format("TestJsons/{0}", path);
            string json = TextFileConcatenation.ReadMultipartFile(path);
#if true
            json = JsonTestUtils.RandomSampleJson(json, maxNumberOfItems: 1);
#endif
            JsonRoundTripsTests.MultiSerializationRoundTrip(json);
        }
        #endregion

        private enum SerializationFormat
        {
            Text,
            Binary,
            NewtonsoftText,
            BinaryWithDictionaryEncoding,
        }

        private static void MultiSerializationRoundTrip(string json)
        {
            foreach (SerializationFormat sourceFormat in Enum.GetValues(typeof(SerializationFormat)))
            {
                foreach (SerializationFormat destinationFormat in Enum.GetValues(typeof(SerializationFormat)))
                {
                    PerformRoundTrip(sourceFormat, destinationFormat, json);
                }
            }
        }

        private static void PerformRoundTrip(SerializationFormat sourceFormat, SerializationFormat destinationFormat, string json)
        {
            IJsonReader reader;
            switch (sourceFormat)
            {
                case SerializationFormat.Text:
                    reader = JsonReader.Create(Encoding.UTF8.GetBytes(json));
                    break;
                case SerializationFormat.Binary:
                    reader = JsonReader.Create(JsonTestUtils.ConvertTextToBinary(json));
                    break;
                case SerializationFormat.NewtonsoftText:
                    reader = NewtonsoftToCosmosDBReader.CreateFromString(json);
                    break;
                case SerializationFormat.BinaryWithDictionaryEncoding:
                    JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 128);
                    reader = JsonReader.Create(JsonTestUtils.ConvertTextToBinary(json, jsonStringDictionary), jsonStringDictionary);
                    break;
                default:
                    throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: {sourceFormat}");
            }

            IJsonNavigator navigator;
            switch (sourceFormat)
            {
                case SerializationFormat.Text:
                    navigator = JsonNavigator.Create(Encoding.UTF8.GetBytes(json));
                    break;
                case SerializationFormat.Binary:
                    navigator = JsonNavigator.Create(JsonTestUtils.ConvertTextToBinary(json));
                    break;
                case SerializationFormat.NewtonsoftText:
                    navigator = new JsonNewtonsoftNavigator(json);
                    break;
                case SerializationFormat.BinaryWithDictionaryEncoding:
                    JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 128);
                    navigator = JsonNavigator.Create(JsonTestUtils.ConvertTextToBinary(json, jsonStringDictionary), jsonStringDictionary);
                    break;
                default:
                    throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: {sourceFormat}");
            }

            object[] sources = new object[] { reader, navigator };
            foreach (object source in sources)
            {
                IJsonWriter writer;
                IJsonWriter writer2;
                JsonStringDictionary jsonStringDictionary;
                switch (destinationFormat)
                {
                    case SerializationFormat.Text:
                        writer = JsonWriter.Create(JsonSerializationFormat.Text);
                        writer2 = JsonWriter.Create(JsonSerializationFormat.Text);
                        jsonStringDictionary = null;
                        break;

                    case SerializationFormat.Binary:
                        writer = JsonWriter.Create(JsonSerializationFormat.Binary);
                        writer2 = JsonWriter.Create(JsonSerializationFormat.Binary);
                        jsonStringDictionary = null;
                        break;

                    case SerializationFormat.NewtonsoftText:
                        writer = NewtonsoftToCosmosDBWriter.CreateTextWriter();
                        writer2 = NewtonsoftToCosmosDBWriter.CreateTextWriter();
                        jsonStringDictionary = null;
                        break;

                    case SerializationFormat.BinaryWithDictionaryEncoding:
                        jsonStringDictionary = new JsonStringDictionary(capacity: 128);
                        writer = JsonWriter.Create(JsonSerializationFormat.Binary, jsonStringDictionary);
                        writer2 = JsonWriter.Create(JsonSerializationFormat.Binary, new JsonStringDictionary(capacity: 128));
                        break;

                    default:
                        throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}");
                }

                switch (source)
                {
                    case IJsonReader sourceReader:
                        writer.WriteAll(sourceReader);
                        break;

                    case IJsonNavigator sourceNavigator:
                        writer.WriteJsonNode(sourceNavigator, sourceNavigator.GetRootNode());
                        sourceNavigator.WriteTo(sourceNavigator.GetRootNode(), writer2);
                        Assert.IsTrue(writer.GetResult().Span.SequenceEqual(writer2.GetResult().Span));
                        break;

                    default:
                        Assert.Fail("Failed to downcast source type.");
                        break;
                }

                string result;
                switch (writer.SerializationFormat)
                {
                    case JsonSerializationFormat.Text:
                        result = Utf8String.UnsafeFromUtf8BytesNoValidation(writer.GetResult()).ToString();
                        break;

                    case JsonSerializationFormat.Binary:
                        result = JsonTestUtils.ConvertBinaryToText(writer.GetResult(), jsonStringDictionary);
                        break;

                    default:
                        throw new ArgumentException();
                }

                string normalizedResult = JsonRoundTripsTests.NewtonsoftFormat(result);
                string normalizedJson = JsonRoundTripsTests.NewtonsoftFormat(json);

                Assert.AreEqual(normalizedJson, normalizedResult);
            }
        }


        private static string NewtonsoftFormat(string json)
        {
            NewtonsoftToCosmosDBReader newtonsoftReader = NewtonsoftToCosmosDBReader.CreateFromString(json);
            NewtonsoftToCosmosDBWriter newtonsoftWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
            newtonsoftWriter.WriteAll(newtonsoftReader);
            return Encoding.UTF8.GetString(newtonsoftWriter.GetResult().ToArray());
        }

        private static void PerformRoundTripTest(string input, IReadOnlyList<JsonToken> tokens)
        {
            // Do the actual roundtrips
            JsonRoundTripsTests.MultiSerializationRoundTrip(input);
        }
    }
}
