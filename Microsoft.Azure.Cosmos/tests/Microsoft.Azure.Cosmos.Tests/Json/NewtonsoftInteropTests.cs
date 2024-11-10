//-----------------------------------------------------------------------
// <copyright file="NewtonsoftInteropTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class NewtonsoftInteropTests
    {
        private static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
        };

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
            bool value = true;
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<bool>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            bool value = false;
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<bool>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            object value = null;
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<object>(value);
        }
        #endregion
        #region Numbers
        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            int value = 1337;
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<int>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            double value = 1337.0;
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<double>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            double value = -1337.0;
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<double>(value);
        }
        #endregion
        #region Strings
        [TestMethod]
        [Owner("brchon")]
        public void EmptyStringTest()
        {
            string value = "";
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<string>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string value = "Hello World";
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<string>(value);
        }
        #endregion
        #region Arrays
        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            object[] value = { };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<object[]>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            int[] value = { -2, -1, 0, 1, 2 };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<int[]>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            double[] value = { 15, 22, 0.1 };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<double[]>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            bool[] value = { true, false };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<bool[]>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            object[] value = { null, null, null };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<object[]>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            object[] value = { new object(), new object() };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<object[]>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            object[,] value = { { }, { } };
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<object[,]>(value);
        }
        #endregion
        #region Objects
        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            object value = new object();
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<object>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            JObject value = new JObject(
                new JProperty("GlossDiv", 1.234),
                new JProperty("title", "example glossary"));
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<JObject>(value);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitivesObjectTest()
        {
            JObject value = new JObject(
                new JProperty("id", Guid.Parse("7029d079-4016-4436-b7da-36c0bae54ff6")),
                new JProperty("double", 0.18963001816981939),
                new JProperty("string", "XCPCFXPHHF"),
                new JProperty("boolean", true),
                new JProperty("null", null),
                new JProperty("datetime", "2526-07-11T18:18:16.4520716"),
                new JProperty("spatialPoint", new JObject(
                    new JProperty("type", "Point"),
                    new JProperty("coordinate", new double[] { 118.9897, -46.6781 }))),
                new JProperty("text", "tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby"));
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<JObject>(value);
        }

        public enum Day { Sun, Mon, Tue, Wed, Thu, Fri, Sat };

        public sealed class ObjectWithAttributes
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; }

            public double Regular { get; }

            [JsonConverter(typeof(UnixDateTimeConverter))]
            public DateTime DateTime { get; }

            [JsonConverter(typeof(StringEnumConverter))]
            public Day Day { get; }

            [JsonConstructor]
            public ObjectWithAttributes(string name, double regular, DateTime dateTime, Day day)
            {
                this.Name = name;
                this.Regular = regular;
                this.DateTime = dateTime;
                this.Day = day;
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectWithAttributesTest()
        {
            ObjectWithAttributes objectWithAttributes = new ObjectWithAttributes("Brandon", 42, new DateTime(1995, 04, 16), Day.Tue);
            NewtonsoftInteropTests.VerifyNewtonsoftInterop<ObjectWithAttributes>(objectWithAttributes);
        }
        #endregion

        private static string NewtonsoftFormat(string json)
        {
            NewtonsoftToCosmosDBReader newtonsoftToCosmosDBReader = NewtonsoftToCosmosDBReader.CreateFromString(json);
            NewtonsoftToCosmosDBWriter newtonsoftToCosmosDBWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
            newtonsoftToCosmosDBReader.WriteAll(newtonsoftToCosmosDBWriter);
            return Encoding.UTF8.GetString(newtonsoftToCosmosDBWriter.GetResult().ToArray());
        }

        private static void VerifyReader<T>(ReadOnlyMemory<byte> payload, T expectedDeserializedValue)
        {
            using (CosmosDBToNewtonsoftReader reader = new CosmosDBToNewtonsoftReader(Cosmos.Json.JsonReader.Create(payload)))
            {
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                T actualDeserializedValue = serializer.Deserialize<T>(reader);
                string expected = JsonConvert.SerializeObject(expectedDeserializedValue, jsonSerializerSettings);
                string actual = JsonConvert.SerializeObject(actualDeserializedValue, jsonSerializerSettings);
                Assert.AreEqual(expected, actual);
            }
        }

        private static void VerifyBinaryReader<T>(T expectedDeserializedValue)
        {
            string stringValue = NewtonsoftInteropTests.NewtonsoftFormat(JsonConvert.SerializeObject(expectedDeserializedValue));
            ReadOnlyMemory<byte> result = JsonTestUtils.ConvertTextToBinary(stringValue);
            NewtonsoftInteropTests.VerifyReader<T>(result, expectedDeserializedValue);
        }

        private static void VerifyTextReader<T>(T expectedDeserializedValue)
        {
            string stringValue = NewtonsoftInteropTests.NewtonsoftFormat(JsonConvert.SerializeObject(expectedDeserializedValue));
            byte[] result = Encoding.UTF8.GetBytes(stringValue);
            NewtonsoftInteropTests.VerifyReader<T>(result, expectedDeserializedValue);
        }

        private static void VerifyWriter<T>(JsonSerializationFormat jsonSerializationFormat, T expectedDeserializedValue)
        {
            using (CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(jsonSerializationFormat))
            {
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                serializer.Serialize(writer, expectedDeserializedValue);

                ReadOnlyMemory<byte> result = writer.GetResult();
                string actualSerializedValue = jsonSerializationFormat == JsonSerializationFormat.Binary
                    ? JsonTestUtils.ConvertBinaryToText(result)
                    : Utf8Span.UnsafeFromUtf8BytesNoValidation(result.Span).ToString();
                actualSerializedValue = NewtonsoftInteropTests.NewtonsoftFormat(actualSerializedValue);
                string expectedSerializedValue = NewtonsoftInteropTests.NewtonsoftFormat(
                    JsonConvert.SerializeObject(
                        expectedDeserializedValue));
                Assert.AreEqual(expectedSerializedValue, actualSerializedValue);
            }
        }

        private static void VerifyTextWriter<T>(T expectedDeserializedValue)
        {
            NewtonsoftInteropTests.VerifyWriter<T>(JsonSerializationFormat.Text, expectedDeserializedValue);
        }

        private static void VerifyBinaryWriter<T>(T expectedDeserializedValue)
        {
            NewtonsoftInteropTests.VerifyWriter<T>(JsonSerializationFormat.Binary, expectedDeserializedValue);
        }

        private static void VerifyNewtonsoftInterop<T>(T expectedDeserializedValue)
        {
            NewtonsoftInteropTests.VerifyBinaryReader<T>(expectedDeserializedValue);
            NewtonsoftInteropTests.VerifyTextReader<T>(expectedDeserializedValue);
            NewtonsoftInteropTests.VerifyBinaryWriter<T>(expectedDeserializedValue);
            NewtonsoftInteropTests.VerifyTextWriter<T>(expectedDeserializedValue);
        }

        /// <summary>
        /// Converts a DateTime object to and from JSON.
        /// DateTime is represented as the total number of seconds
        /// that have elapsed since January 1, 1970 (midnight UTC/GMT), 
        /// not counting leap seconds (in ISO 8601: 1970-01-01T00:00:00Z).
        /// </summary>
        private sealed class UnixDateTimeConverter : DateTimeConverterBase
        {
            private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            /// <summary>
            /// Writes the JSON representation of the DateTime object.
            /// </summary>
            /// <param name="writer">The Newtonsoft.Json.JsonWriter to write to.</param>
            /// <param name="value">The value.</param>
            /// <param name="serializer">The calling serializer.</param>
            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            {
                if (value is DateTime time)
                {
                    Int64 totalSeconds = (Int64)(time - UnixStartTime).TotalSeconds;
                    writer.WriteValue(totalSeconds);
                }
                else
                {
                    throw new ArgumentException(RMResources.DateTimeConverterInvalidDateTime, "value");
                }
            }

            /// <summary>
            /// Reads the JSON representation of the DateTime object.
            /// </summary>
            /// <param name="reader">The Newtonsoft.Json.JsonReader to read from.</param>
            /// <param name="objectType">Type of the object.</param>
            /// <param name="existingValue">The existing value of object being read.</param>
            /// <param name="serializer">The calling serializer.</param>
            /// <returns>
            /// The DateTime object value.
            /// </returns>
            public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
            {
                if (reader.TokenType != Newtonsoft.Json.JsonToken.Integer && reader.TokenType != Newtonsoft.Json.JsonToken.Float)
                {
                    throw new Exception(RMResources.DateTimeConverterInvalidReaderValue);
                }

                double totalSeconds;
                try
                {
                    totalSeconds = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    throw new Exception(RMResources.DateTimeConveterInvalidReaderDoubleValue);
                }

                return UnixStartTime.AddSeconds(totalSeconds);
            }
        }
    }
}