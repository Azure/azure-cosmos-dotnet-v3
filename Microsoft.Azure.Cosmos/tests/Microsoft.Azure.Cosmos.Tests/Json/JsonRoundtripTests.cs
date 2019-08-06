//-----------------------------------------------------------------------
// <copyright file="JsonRoundTripsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Functional")]
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
            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Boolean(true)
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";
            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Boolean(false)
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";
            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Null()
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

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Number(1337)
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.7";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Number(1337.7)
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string input = "-1337.7";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Number(-1337.7)
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string input2 = "6.02252E+23";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Number(6.02252e23)
            };

            this.PerformRoundTripTest(input2, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithPostitiveExponent()
        {
            string input2 = "6.02252E+23";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Number(6.02252e+23)
            };

            this.PerformRoundTripTest(input2, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithNegativeExponent()
        {
            string input2 = "6.02252E-23";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.Number(6.02252e-23)
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
            JsonTokenInfo[] token =
            {
                JsonTokenInfo.String(string.Empty)
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string input = "\"Hello World\"";
            JsonTokenInfo[] token =
            {
                JsonTokenInfo.String("Hello World")
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

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(-2),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(0),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(2),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(15),
                JsonTokenInfo.Number(22),
                JsonTokenInfo.Number(0.1),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.1, -1, -1.1, 1, 2, \"hello\", null, true, false]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(0),
                JsonTokenInfo.Number(0.1),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(-1.1),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(2),
                JsonTokenInfo.String("hello"),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
                JsonTokenInfo.ArrayEnd(),
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
                JsonTokenInfo[] token =
                {
                     JsonTokenInfo.String(escapeCharacter.Item2),
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

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String("hello"),
                JsonTokenInfo.String("my"),
                JsonTokenInfo.String("name"),
                JsonTokenInfo.String("is"),
                JsonTokenInfo.ArrayEnd(),
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

            JsonTokenInfo[] token =
            {
                 JsonTokenInfo.String(expectedString),
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

            JsonTokenInfo[] token =
            {
                 JsonTokenInfo.String(expectedString),
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

            JsonTokenInfo[] token =
            {
                 JsonTokenInfo.ObjectStart(),
                 JsonTokenInfo.ObjectEnd(),
            };

            this.PerformRoundTripTest(input, token);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("GlossDiv"),
                JsonTokenInfo.Number(10),
                JsonTokenInfo.FieldName("title"),
                JsonTokenInfo.String("example glossary"),
                JsonTokenInfo.ObjectEnd(),
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

            JsonTokenInfo[] token =
            {
                JsonTokenInfo.ObjectStart(),

                    JsonTokenInfo.FieldName("id"),
                    JsonTokenInfo.String("7029d079-4016-4436-b7da-36c0bae54ff6"),

                    JsonTokenInfo.FieldName("double"),
                    JsonTokenInfo.Number(0.18963001816981939),

                    JsonTokenInfo.FieldName("int"),
                    JsonTokenInfo.Number(-1330192615),

                    JsonTokenInfo.FieldName("string"),
                    JsonTokenInfo.String("XCPCFXPHHF"),

                    JsonTokenInfo.FieldName("boolean"),
                    JsonTokenInfo.Boolean(true),

                    JsonTokenInfo.FieldName("null"),
                    JsonTokenInfo.Null(),

                    JsonTokenInfo.FieldName("datetime"),
                    JsonTokenInfo.String("2526-07-11T18:18:16.4520716"),

                    JsonTokenInfo.FieldName("spatialPoint"),
                    JsonTokenInfo.ObjectStart(),
                        JsonTokenInfo.FieldName("type"),
                        JsonTokenInfo.String("Point"),

                        JsonTokenInfo.FieldName("coordinates"),
                        JsonTokenInfo.ArrayStart(),
                            JsonTokenInfo.Number(118.9897),
                            JsonTokenInfo.Number(-46.6781),
                        JsonTokenInfo.ArrayEnd(),
                    JsonTokenInfo.ObjectEnd(),

                    JsonTokenInfo.FieldName("text"),
                    JsonTokenInfo.String("tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby"),
                JsonTokenInfo.ObjectEnd(),
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
            JsonTokenInfo[] minByteTokens =
            {
                JsonTokenInfo.Number(byte.MinValue)
            };

            this.PerformRoundTripTest(minByteInput, minByteTokens);

            // max byte
            string maxByteInput = "255";
            JsonTokenInfo[] maxByteTokens =
            {
                JsonTokenInfo.Number(byte.MaxValue)
            };

            this.PerformRoundTripTest(maxByteInput, maxByteTokens);

            // min short
            string minShortInput = "-32768";
            JsonTokenInfo[] minShortTokens =
            {
                JsonTokenInfo.Number(short.MinValue)
            };

            this.PerformRoundTripTest(minShortInput, minShortTokens);

            // max short
            string maxShortInput = "32767";
            JsonTokenInfo[] maxShortTokens =
            {
                JsonTokenInfo.Number(short.MaxValue)
            };

            this.PerformRoundTripTest(maxShortInput, maxShortTokens);

            // min int
            string minIntInput = "-2147483648";
            JsonTokenInfo[] minIntTokens =
            {
                JsonTokenInfo.Number(int.MinValue)
            };

            this.PerformRoundTripTest(minIntInput, minIntTokens);

            // max int
            string maxIntInput = "2147483647";
            JsonTokenInfo[] maxIntTokens =
            {
                JsonTokenInfo.Number(int.MaxValue)
            };

            this.PerformRoundTripTest(maxIntInput, maxIntTokens);

            // min long
            string minLongInput = "-9223372036854775808";
            JsonTokenInfo[] minLongTokens =
            {
                JsonTokenInfo.Number(long.MinValue)
            };

            this.PerformRoundTripTest(minLongInput, minLongTokens);

            // max long
            string maxLongInput = "9223372036854775807";
            JsonTokenInfo[] maxLongTokens =
            {
                JsonTokenInfo.Number(long.MaxValue)
            };

            this.PerformRoundTripTest(maxLongInput, maxLongTokens);

            // min double
            string minDoubleInput = "-1.7976931348623157E+308";
            JsonTokenInfo[] minDoubleTokens =
            {
                JsonTokenInfo.Number(double.MinValue)
            };

            this.PerformRoundTripTest(minDoubleInput, minDoubleTokens);

            // max double
            string maxDoubleInput = "1.7976931348623157E+308";
            JsonTokenInfo[] maxDoubleTokens =
            {
                JsonTokenInfo.Number(double.MaxValue)
            };

            this.PerformRoundTripTest(maxDoubleInput, maxDoubleTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayLengthLimitsTest()
        {
            // empty array 
            string emptyArrayInput = "[]";
            JsonTokenInfo[] emptyArrayTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd()
            };

            this.PerformRoundTripTest(emptyArrayInput, emptyArrayTokens);

            // single item array 
            string singleItemArrayInput = @"[""a""]";
            JsonTokenInfo[] singleItemArrayTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String("a"),
                JsonTokenInfo.ArrayEnd()
            };

            this.PerformRoundTripTest(singleItemArrayInput, singleItemArrayTokens);

            // max 1 byte length array
            string maxByteLengthPayload = new string('a', byte.MaxValue - 1 - 1);
            string maxByteLengthInput = @"[""" + maxByteLengthPayload + @"""]";
            JsonTokenInfo[] maxByteLengthTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String(maxByteLengthPayload),
                JsonTokenInfo.ArrayEnd()
            };

            this.PerformRoundTripTest(maxByteLengthInput, maxByteLengthTokens);

            // max 2 byte length array
            string maxUShortLengthPayload = new string('a', ushort.MaxValue - 1 - 2);
            string maxUShortLengthInput = @"[""" + maxUShortLengthPayload + @"""]";
            JsonTokenInfo[] maxUShortLengthTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String(maxUShortLengthPayload),
                JsonTokenInfo.ArrayEnd()
            };

            this.PerformRoundTripTest(maxUShortLengthInput, maxUShortLengthTokens);

            // max 4 byte length array
            string maxUIntLengthPayload = new string('a', ushort.MaxValue);
            string maxUIntLengthInput = @"[""" + maxUIntLengthPayload + @"""]";
            JsonTokenInfo[] maxUIntLengthTokens =
            {
                JsonTokenInfo.ArrayStart(),
                // 2 of them just to go past int.MaxValue but < uint.MaxValue
                JsonTokenInfo.String(maxUIntLengthPayload),
                JsonTokenInfo.ArrayEnd()
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
            this.RoundTripTestCuratedJson("countries.json");
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
            this.RoundTripTestCuratedJson("lastfm.json");
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
            this.RoundTripTestCuratedJson("NutritionData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            this.RoundTripTestCuratedJson("runsCollection.json");
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
            this.RoundTripTestCuratedJson("states_legislators.json");
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
            this.RoundTripTestCuratedJson("TicinoErrorBuckets.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            this.RoundTripTestCuratedJson("twitter_data.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            this.RoundTripTestCuratedJson("ups1.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            this.RoundTripTestCuratedJson("XpertEvents.json");
        }

        // Checks to see if we can go from a JsonReader to a NewtonsoftWriter and get back the original document and visa versa
        private void RoundTripTestCuratedJson(string filename)
        {
            string path = string.Format("TestJsons/{0}", filename);
            string json = File.ReadAllText(path);
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
                            reader = new JsonNewtonsoftNewtonsoftTextReader(json);
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
                        default:
                            throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: {sourceFormat}");
                    }

                    object[] sources = new object[] { reader, navigator };
                    foreach (object source in sources)
                    {
                        IJsonWriter writer;
                        switch (destinationFormat)
                        {
                            case SerializationFormat.Text:
                                writer = JsonWriter.Create(JsonSerializationFormat.Text);
                                break;
                            case SerializationFormat.Binary:
                                writer = JsonWriter.Create(JsonSerializationFormat.Binary);
                                break;
                            case SerializationFormat.NewtonsoftText:
                                writer = new JsonNewtonsoftNewtonsoftTextWriter();
                                break;
                            default:
                                throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}");
                        }

                        Stopwatch stopwatch = Stopwatch.StartNew();
                        if (source is IJsonReader)
                        {
                            IJsonReader sourceReader = source as IJsonReader;
                            writer.WriteAll(sourceReader);
                        }
                        else if (source is IJsonNavigator)
                        {
                            IJsonNavigator sourceNavigator = source as IJsonNavigator;
                            writer.WriteJsonNode(sourceNavigator, sourceNavigator.GetRootNode());
                        }
                        stopwatch.Stop();

                        string result;
                        switch (writer.SerializationFormat)
                        {
                            case JsonSerializationFormat.Text:
                                result = Encoding.UTF8.GetString(writer.GetResult());
                                break;
                            case JsonSerializationFormat.Binary:
                                result = JsonTestUtils.ConvertBinaryToText(writer.GetResult());
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

        private enum NewtonsoftWrapperFormat
        {
            NewtonsoftText,
            CosmosDBText,
            CosmosDBBinary,
        }

        private void NewtonsoftWrapperRoundTrip(string json)
        {
            // Normalize the json to get rid of any formatting issues
            json = this.NewtonsoftFormat(json);

            foreach (NewtonsoftWrapperFormat sourceFormat in Enum.GetValues(typeof(NewtonsoftWrapperFormat)))
            {
                foreach (NewtonsoftWrapperFormat destinationFormat in Enum.GetValues(typeof(NewtonsoftWrapperFormat)))
                {
                    IJsonReader reader;
                    switch (sourceFormat)
                    {
                        case NewtonsoftWrapperFormat.NewtonsoftText:
                            reader = new JsonNewtonsoftNewtonsoftTextReader(json);
                            break;
                        case NewtonsoftWrapperFormat.CosmosDBText:
                            reader = new JsonNewtonsoftCosmosDBTextReader(json);
                            break;
                        case NewtonsoftWrapperFormat.CosmosDBBinary:
                            reader = new JsonNewtonsoftCosmosDBBinaryReader(json);
                            break;
                        default:
                            throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: {sourceFormat}");
                    }

                    IJsonWriter writer;
                    switch (destinationFormat)
                    {
                        case NewtonsoftWrapperFormat.NewtonsoftText:
                            writer = new JsonNewtonsoftNewtonsoftTextWriter();
                            break;
                        case NewtonsoftWrapperFormat.CosmosDBText:
                            writer = new JsonNewtonsoftCosmosDBTextWriter();
                            break;
                        case NewtonsoftWrapperFormat.CosmosDBBinary:
                            writer = new JsonNewtonsoftCosmosDBBinaryWriter();
                            break;
                        default:
                            throw new ArgumentException($"Unexpected {nameof(sourceFormat)} of type: {sourceFormat}");
                    }

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    writer.WriteAll(reader);
                    stopwatch.Stop();

                    string result;
                    switch (writer.SerializationFormat)
                    {
                        case JsonSerializationFormat.Text:
                            result = Encoding.UTF8.GetString(writer.GetResult());
                            break;
                        case JsonSerializationFormat.Binary:
                            result = JsonTestUtils.ConvertBinaryToText(writer.GetResult());
                            break;
                        default:
                            throw new ArgumentException();
                    }

                    result = this.NewtonsoftFormat(result);
                    Assert.AreEqual(json, result);

                    Console.WriteLine($"{sourceFormat} Reader to {destinationFormat} Writer took {stopwatch.ElapsedMilliseconds}ms");
                }
            }
        }

        private string FormatJson(string json)
        {
            // Feed the json through our reader and writer once to remove and formatting and escaping differences
            IJsonReader jsonReaderFormatter = JsonReader.Create(Encoding.UTF8.GetBytes(json));
            IJsonWriter jsonWriterFormatter = JsonWriter.Create(JsonSerializationFormat.Text);
            jsonWriterFormatter.WriteAll(jsonReaderFormatter);
            string formattedJson = Encoding.UTF8.GetString(jsonWriterFormatter.GetResult());
            return formattedJson;
        }

        private void TextRoundTrip(string input)
        {
            string formattedJson = this.FormatJson(input);

            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(formattedJson));
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            jsonWriter.WriteAll(jsonReader);
            string jsonFromWriter = Encoding.UTF8.GetString(jsonWriter.GetResult());
            Assert.AreEqual(formattedJson, jsonFromWriter);
        }

        private string NewtonsoftFormat(string json)
        {
            JsonNewtonsoftReader newtonsoftReader = new JsonNewtonsoftNewtonsoftTextReader(json);
            JsonNewtonsoftWriter newtonsoftWriter = new JsonNewtonsoftNewtonsoftTextWriter();
            newtonsoftWriter.WriteAll(newtonsoftReader);
            return Encoding.UTF8.GetString(newtonsoftWriter.GetResult());
        }

        private void PerformRoundTripTest(string input, JsonTokenInfo[] tokens)
        {
            // Do the actual roundtrips
            this.MultiSerializationRoundTrip(input);
            // this.NewtonsoftWrapperRoundTrip(input);
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
            string output = Encoding.UTF8.GetString(jsonWriter.GetResult());

            string inputNoWhitespace = Regex.Replace(input, @"\s+", "");
            string outputNoWhitespace = Regex.Replace(output, @"\s+", "");

            Assert.AreEqual(inputNoWhitespace, outputNoWhitespace);
        }

        private void TestWriterToReader(JsonTokenInfo[] tokens)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            JsonPerfMeasurement.MeasureWritePerformance(tokens, jsonWriter);
            string writerResults = Encoding.UTF8.GetString(jsonWriter.GetResult());
            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(writerResults));
            JsonTokenInfo[] tokenArrayFromReader = JsonPerfMeasurement.Tokenize(jsonReader, writerResults);
            tokenArrayFromReader.SequenceEqual(tokens);
        }
    }
}
