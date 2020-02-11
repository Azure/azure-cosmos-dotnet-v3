//-----------------------------------------------------------------------
// <copyright file="JsonRoundTripsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input2, token);
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

            this.PerformRoundTripTest(input2, token);
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

            this.PerformRoundTripTest(input2, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

                this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(unicodeString, token);
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

            this.PerformRoundTripTest(unicodeString, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(input, token);
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

            this.PerformRoundTripTest(minByteInput, minByteTokens);

            // max byte
            string maxByteInput = "255";
            JsonToken[] maxByteTokens =
            {
                JsonToken.Number(byte.MaxValue)
            };

            this.PerformRoundTripTest(maxByteInput, maxByteTokens);

            // min short
            string minShortInput = "-32768";
            JsonToken[] minShortTokens =
            {
                JsonToken.Number(short.MinValue)
            };

            this.PerformRoundTripTest(minShortInput, minShortTokens);

            // max short
            string maxShortInput = "32767";
            JsonToken[] maxShortTokens =
            {
                JsonToken.Number(short.MaxValue)
            };

            this.PerformRoundTripTest(maxShortInput, maxShortTokens);

            // min int
            string minIntInput = "-2147483648";
            JsonToken[] minIntTokens =
            {
                JsonToken.Number(int.MinValue)
            };

            this.PerformRoundTripTest(minIntInput, minIntTokens);

            // max int
            string maxIntInput = "2147483647";
            JsonToken[] maxIntTokens =
            {
                JsonToken.Number(int.MaxValue)
            };

            this.PerformRoundTripTest(maxIntInput, maxIntTokens);

            // min long
            string minLongInput = "-9223372036854775808";
            JsonToken[] minLongTokens =
            {
                JsonToken.Number(long.MinValue)
            };

            this.PerformRoundTripTest(minLongInput, minLongTokens);

            // max long
            string maxLongInput = "9223372036854775807";
            JsonToken[] maxLongTokens =
            {
                JsonToken.Number(long.MaxValue)
            };

            this.PerformRoundTripTest(maxLongInput, maxLongTokens);

            // min double
            string minDoubleInput = "-1.7976931348623157E+308";
            JsonToken[] minDoubleTokens =
            {
                JsonToken.Number(double.MinValue)
            };

            this.PerformRoundTripTest(minDoubleInput, minDoubleTokens);

            // max double
            string maxDoubleInput = "1.7976931348623157E+308";
            JsonToken[] maxDoubleTokens =
            {
                JsonToken.Number(double.MaxValue)
            };

            this.PerformRoundTripTest(maxDoubleInput, maxDoubleTokens);
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

            this.PerformRoundTripTest(emptyArrayInput, emptyArrayTokens);

            // single item array 
            string singleItemArrayInput = @"[""a""]";
            JsonToken[] singleItemArrayTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String("a"),
                JsonToken.ArrayEnd()
            };

            this.PerformRoundTripTest(singleItemArrayInput, singleItemArrayTokens);

            // max 1 byte length array
            string maxByteLengthPayload = new string('a', byte.MaxValue - 1 - 1);
            string maxByteLengthInput = @"[""" + maxByteLengthPayload + @"""]";
            JsonToken[] maxByteLengthTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String(maxByteLengthPayload),
                JsonToken.ArrayEnd()
            };

            this.PerformRoundTripTest(maxByteLengthInput, maxByteLengthTokens);

            // max 2 byte length array
            string maxUShortLengthPayload = new string('a', ushort.MaxValue - 1 - 2);
            string maxUShortLengthInput = @"[""" + maxUShortLengthPayload + @"""]";
            JsonToken[] maxUShortLengthTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String(maxUShortLengthPayload),
                JsonToken.ArrayEnd()
            };

            this.PerformRoundTripTest(maxUShortLengthInput, maxUShortLengthTokens);

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

            this.PerformRoundTripTest(maxUIntLengthInput, maxUIntLengthTokens);
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
            json = JsonTestUtils.RandomSampleJson(json);
#endif
            this.MultiSerializationRoundTrip(json);
        }
#endregion

        private enum SerializationFormat
        {
            Text,
            Binary,
            NewtonsoftText,
            BinaryWithDictionaryEncoding,
        }

        private void MultiSerializationRoundTrip(string json)
        {
            // Normalize the json to get rid of any formatting issues
            json = this.NewtonsoftFormat(json);

            foreach (SerializationFormat sourceFormat in Enum.GetValues(typeof(SerializationFormat)))
            {
                foreach (SerializationFormat destinationFormat in Enum.GetValues(typeof(SerializationFormat)))
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
                        JsonStringDictionary jsonStringDictionary;
                        switch (destinationFormat)
                        {
                            case SerializationFormat.Text:
                                writer = JsonWriter.Create(JsonSerializationFormat.Text);
                                jsonStringDictionary = null;
                                break;
                            case SerializationFormat.Binary:
                                writer = JsonWriter.Create(JsonSerializationFormat.Binary);
                                jsonStringDictionary = null;
                                break;
                            case SerializationFormat.NewtonsoftText:
                                writer = NewtonsoftToCosmosDBWriter.CreateTextWriter();
                                jsonStringDictionary = null;
                                break;
                            case SerializationFormat.BinaryWithDictionaryEncoding:
                                jsonStringDictionary = new JsonStringDictionary(capacity: 128);
                                writer = JsonWriter.Create(JsonSerializationFormat.Binary, jsonStringDictionary);
                                break;
                            default:
                                throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}");
                        }

                        Stopwatch stopwatch = Stopwatch.StartNew();
                        switch (source)
                        {
                            case IJsonReader sourceReader:
                                writer.WriteAll(sourceReader);
                                break;

                            case IJsonNavigator sourceNavigator:
                                writer.WriteJsonNode(sourceNavigator, sourceNavigator.GetRootNode());
                                break;

                            default:
                                Assert.Fail("Failed to downcast source type.");
                                break;
                        }
                        stopwatch.Stop();

                        string result;
                        switch (writer.SerializationFormat)
                        {
                            case JsonSerializationFormat.Text:
                                result = Encoding.UTF8.GetString(writer.GetResult().ToArray());
                                break;
                            case JsonSerializationFormat.Binary:
                                result = JsonTestUtils.ConvertBinaryToText(writer.GetResult().ToArray(), jsonStringDictionary);
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        result = this.NewtonsoftFormat(result);

                        Assert.AreEqual(json, result);
                        string sourceType = (source is IJsonReader) ? "Reader" : "Navigator";
                        Console.WriteLine($"{sourceFormat} {sourceType} to {destinationFormat} Writer took {stopwatch.ElapsedMilliseconds}ms");
                    }
                }
            }
        }

        private string FormatJson(string json)
        {
            // Feed the json through our reader and writer once to remove and formatting and escaping differences
            IJsonReader jsonReaderFormatter = JsonReader.Create(Encoding.UTF8.GetBytes(json));
            IJsonWriter jsonWriterFormatter = JsonWriter.Create(JsonSerializationFormat.Text);
            jsonWriterFormatter.WriteAll(jsonReaderFormatter);
            string formattedJson = Encoding.UTF8.GetString(jsonWriterFormatter.GetResult().ToArray());
            return formattedJson;
        }

        private void TextRoundTrip(string input)
        {
            string formattedJson = this.FormatJson(input);

            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(formattedJson));
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            jsonWriter.WriteAll(jsonReader);
            string jsonFromWriter = Encoding.UTF8.GetString(jsonWriter.GetResult().ToArray());
            Assert.AreEqual(formattedJson, jsonFromWriter);
        }

        private string NewtonsoftFormat(string json)
        {
            NewtonsoftToCosmosDBReader newtonsoftReader = NewtonsoftToCosmosDBReader.CreateFromString(json);
            NewtonsoftToCosmosDBWriter newtonsoftWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
            newtonsoftWriter.WriteAll(newtonsoftReader);
            return Encoding.UTF8.GetString(newtonsoftWriter.GetResult().ToArray());
        }

        private void PerformRoundTripTest(string input, JsonToken[] tokens)
        {
            // Do the actual roundtrips
            this.MultiSerializationRoundTrip(input);
        }

        /// <summary>
        /// Reads all the tokens from the input string using a JsonReader and writes them to a JsonReader and sees if we get back the same result.
        /// </summary>
        /// <param name="input">The input to read from.</param>
        private void TestReaderToWriter(string input)
        {
            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(input));
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);

            jsonWriter.WriteAll(jsonReader);
            string output = Encoding.UTF8.GetString(jsonWriter.GetResult().ToArray());

            string inputNoWhitespace = Regex.Replace(input, @"\s+", "");
            string outputNoWhitespace = Regex.Replace(output, @"\s+", "");

            Assert.AreEqual(inputNoWhitespace, outputNoWhitespace);
        }

        private void TestWriterToReader(JsonToken[] tokens)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            JsonPerfMeasurement.MeasureWritePerformance(tokens, jsonWriter);
            string writerResults = Encoding.UTF8.GetString(jsonWriter.GetResult().ToArray());
            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(writerResults));
            JsonToken[] tokenArrayFromReader = JsonPerfMeasurement.Tokenize(jsonReader, writerResults);
            tokenArrayFromReader.SequenceEqual(tokens);
        }
    }
}
