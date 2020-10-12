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
        #region Literals
        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            string input = "true";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }
        #endregion
        #region Numbers
        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string input = "1337";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.7";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string input = "-1337.7";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string input = "6.02252E+23";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithPostitiveExponent()
        {
            string input = "6.02252E+23";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithNegativeExponent()
        {
            string input = "6.02252E-23";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }
        #endregion
        #region Strings
        [TestMethod]
        [Owner("brchon")]
        public void EmptyStringTest()
        {
            string input = "\"\"";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string input = "\"Hello World\"";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }
        #endregion
        #region Arrays
        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.1, -1, -1.1, 1, 2, \"hello\", null, true, false]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";
            JsonRoundTripsTests.PerformRoundTripTest(input);
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
                JsonRoundTripsTests.PerformRoundTripTest(input);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void WhitespaceCharacterTest()
        {
            string input = "[" + " " + "\"hello\"" + "," + "\t" + "\"my\"" + "\r" + "," + "\"name\"" + "\n" + "," + "\"is\"" + "]";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnicodeTest()
        {
            // the user might literally paste a unicode character into the json.
            string unicodeString = "\"€\"";
            JsonRoundTripsTests.PerformRoundTripTest(unicodeString);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmojiUTF32Test()
        {
            // the user might literally paste a utf 32 character (like the poop emoji).
            string unicodeString = "\"💩\"";

            JsonRoundTripsTests.PerformRoundTripTest(unicodeString);
        }
        #endregion
        #region Objects
        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            string input = "{}";
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";
            JsonRoundTripsTests.PerformRoundTripTest(input);
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
            JsonRoundTripsTests.PerformRoundTripTest(input);
        }
        #endregion
        #region Limits
        [TestMethod]
        [Owner("brchon")]
        public void NumberLimitsTest()
        {
            // min byte
            string minByteInput = "0";
            JsonRoundTripsTests.PerformRoundTripTest(minByteInput);

            // max byte
            string maxByteInput = "255";
            JsonRoundTripsTests.PerformRoundTripTest(maxByteInput);

            // min short
            string minShortInput = "-32768";
            JsonRoundTripsTests.PerformRoundTripTest(minShortInput);

            // max short
            string maxShortInput = "32767";
            JsonRoundTripsTests.PerformRoundTripTest(maxShortInput);

            // min int
            string minIntInput = "-2147483648";
            JsonRoundTripsTests.PerformRoundTripTest(minIntInput);

            // max int
            string maxIntInput = "2147483647";
            JsonRoundTripsTests.PerformRoundTripTest(maxIntInput);

            // min long
            string minLongInput = "-9223372036854775808";
            JsonRoundTripsTests.PerformRoundTripTest(minLongInput);

            // max long
            string maxLongInput = "9223372036854775807";
            JsonRoundTripsTests.PerformRoundTripTest(maxLongInput);

            // min double
            string minDoubleInput = "-1.7976931348623157E+308";
            JsonRoundTripsTests.PerformRoundTripTest(minDoubleInput);

            // max double
            string maxDoubleInput = "1.7976931348623157E+308";
            JsonRoundTripsTests.PerformRoundTripTest(maxDoubleInput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayLengthLimitsTest()
        {
            // empty array 
            string emptyArrayInput = "[]";
            JsonRoundTripsTests.PerformRoundTripTest(emptyArrayInput);

            // single item array 
            string singleItemArrayInput = @"[""a""]";
            JsonRoundTripsTests.PerformRoundTripTest(singleItemArrayInput);

            // max 1 byte length array
            string maxByteLengthPayload = new string('a', byte.MaxValue - 1 - 1);
            string maxByteLengthInput = @"[""" + maxByteLengthPayload + @"""]";
            JsonRoundTripsTests.PerformRoundTripTest(maxByteLengthInput);

            // max 2 byte length array
            string maxUShortLengthPayload = new string('a', ushort.MaxValue - 1 - 2);
            string maxUShortLengthInput = @"[""" + maxUShortLengthPayload + @"""]";
            JsonRoundTripsTests.PerformRoundTripTest(maxUShortLengthInput);

            // max 4 byte length array
            string maxUIntLengthPayload = new string('a', ushort.MaxValue);
            string maxUIntLengthInput = @"[""" + maxUIntLengthPayload + @"""]";
            JsonRoundTripsTests.PerformRoundTripTest(maxUIntLengthInput);
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
        [Ignore] // Takes too long
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
            json = JsonTestUtils.RandomSampleJson(json, seed: 42, maxNumberOfItems: 100);
#endif
            JsonRoundTripsTests.MultiSerializationRoundTrip(json);
        }
        #endregion

        private enum SerializationFormat
        {
            Text,
            Binary,
            NewtonsoftText,
            //BinaryWithDictionaryEncoding,
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

        private static void PerformRoundTrip(
            SerializationFormat sourceFormat,
            SerializationFormat destinationFormat,
            string json)
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

                //case SerializationFormat.BinaryWithDictionaryEncoding:
                //    sourceDictionary = new JsonStringDictionary(capacity: 128);
                //    reader = JsonReader.Create(JsonTestUtils.ConvertTextToBinary(json, sourceDictionary), sourceDictionary);
                //    break;

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

                //case SerializationFormat.BinaryWithDictionaryEncoding:
                //    sourceDictionary = new JsonStringDictionary(capacity: 128);
                //    navigator = JsonNavigator.Create(JsonTestUtils.ConvertTextToBinary(json, sourceDictionary), sourceDictionary);
                //    break;

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

                    //case SerializationFormat.BinaryWithDictionaryEncoding:
                    //    jsonStringDictionary = new JsonStringDictionary(capacity: 128);
                    //    if (sourceFormat == SerializationFormat.BinaryWithDictionaryEncoding)
                    //    {
                    //        int index = 0;
                    //        while (sourceDictionary.TryGetStringAtIndex(index++, out UtfAllString value))
                    //        {
                    //            Assert.IsTrue(jsonStringDictionary.TryAddString(value.Utf16String, out _));
                    //        }
                    //    }

                    //    writer = JsonWriter.Create(JsonSerializationFormat.Binary, jsonStringDictionary);
                    //    break;

                    default:
                        throw new ArgumentException($"Unexpected {nameof(destinationFormat)} of type: {destinationFormat}");
                }

                switch (source)
                {
                    case IJsonReader sourceReader:
                        sourceReader.WriteAll(writer);
                        break;

                    case IJsonNavigator sourceNavigator:
                        sourceNavigator.WriteNode(sourceNavigator.GetRootNode(), writer);
                        break;

                    default:
                        Assert.Fail("Failed to downcast source type.");
                        break;
                }

                string result = writer.SerializationFormat switch
                {
                    JsonSerializationFormat.Text => Utf8String.UnsafeFromUtf8BytesNoValidation(writer.GetResult()).ToString(),
                    JsonSerializationFormat.Binary => JsonTestUtils.ConvertBinaryToText(writer.GetResult(), jsonStringDictionary),
                    _ => throw new ArgumentException(),
                };
                string normalizedResult = JsonRoundTripsTests.NewtonsoftFormat(result);
                string normalizedJson = JsonRoundTripsTests.NewtonsoftFormat(json);

                Assert.AreEqual(normalizedJson, normalizedResult);
            }
        }


        private static string NewtonsoftFormat(string json)
        {
            NewtonsoftToCosmosDBReader newtonsoftReader = NewtonsoftToCosmosDBReader.CreateFromString(json);
            NewtonsoftToCosmosDBWriter newtonsoftWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
            newtonsoftReader.WriteAll(newtonsoftWriter);
            return Encoding.UTF8.GetString(newtonsoftWriter.GetResult().ToArray());
        }

        private static void PerformRoundTripTest(string input)
        {
            // Do the actual roundtrips
            JsonRoundTripsTests.MultiSerializationRoundTrip(input);
        }
    }
}
