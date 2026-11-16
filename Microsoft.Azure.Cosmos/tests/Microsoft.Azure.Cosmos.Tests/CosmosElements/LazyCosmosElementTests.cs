//-----------------------------------------------------------------------
// <copyright file="LazyCosmosElementTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for LazyCosmosElementTests.
    /// </summary>
    //[Ignore]
    [TestClass]
    public class LazyCosmosElementTests
    {
        private static List<Person> people;
        private static string serializedPeople;
        private static byte[] bufferedSerializedPeople;

        [ClassInitialize]
        public static void TestInitialize(TestContext testContext)
        {
            int numberOfPeople = 2;
            people = new List<Person>();

            Random random = new Random(1234);
            for (int i = 0; i < numberOfPeople; i++)
            {
                people.Add(Person.CreateRandomPerson(random));
            }

            serializedPeople = JsonConvert.SerializeObject(people, Formatting.None);
            bufferedSerializedPeople = Encoding.UTF8.GetBytes(serializedPeople);
        }

        [TestMethod]
        [DataRow(new byte[] { 0x09 }, DisplayName = "Tab")]
        [DataRow(new byte[] { 0x20 }, DisplayName = "Space")]
        [DataRow(new byte[] { 0x0A }, DisplayName = "LineFeed")]
        [DataRow(new byte[] { 0x0D }, DisplayName = "CarriageReturn")]
        [DataRow(new byte[] { 0x20, 0x09, 0x0D, 0x0A, 0x20 }, DisplayName = "MixedWhitespace")]
        public void CreateFromBuffer_WhitespaceOnlyText_ReturnsFailed(byte[] buffer)
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(buffer);

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(JsonParseException));
            Assert.IsFalse(CosmosElement.TryCreateFromBuffer(buffer, out CosmosElement _));
        }

        [TestMethod]
        public void CreateFromBuffer_EmptyBuffer_ReturnsFailed()
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(ReadOnlyMemory<byte>.Empty);

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(ArgumentException));
        }

        [TestMethod]
        public void CreateFromBuffer_MalformedBinary_ReturnsFailed()
        {
            byte[] buffer = new byte[]
            {
                0x80, 0xff, 0x9e, 0x2b, 0x22, 0x7e, 0x5b, 0x31, 0xe4, 0x2c,
                0x32, 0x2c, 0x33, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x61, 0x00, 0x00, 0x00, 0x00, 0x31, 0x84,
                0x2c, 0x00, 0x22, 0x00, 0x15, 0x00, 0x00, 0x00, 0x00, 0x68,
            };

            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(buffer);

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(JsonParseException));
            Assert.IsFalse(CosmosElement.TryCreateFromBuffer(buffer, out CosmosElement _));
        }

        [TestMethod]
        public void CreateFromBuffer_RandomBuffers_NeverThrow()
        {
            Random random = new Random(Seed: 42);
            for (int i = 0; i < 20000; i++)
            {
                byte[] buffer = new byte[random.Next(1, 48)];
                random.NextBytes(buffer);
                if (i % 2 == 0)
                {
                    buffer[0] = (byte)JsonSerializationFormat.Binary;
                }

                try
                {
                    CosmosElement.Monadic.CreateFromBuffer(buffer);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"CreateFromBuffer threw {ex.GetType().Name} for input {BitConverter.ToString(buffer)}: {ex}");
                }
            }
        }

        [TestMethod]
        public void CreateFromBuffer_TrailingTokens_ReturnsFailed()
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(Encoding.UTF8.GetBytes("1 2"));

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(JsonParseException));
        }

        [TestMethod]
        public void CreateFromBuffer_ValidText_StillSucceeds()
        {
            TryCatch<CosmosObject> result = CosmosElement.Monadic.CreateFromBuffer<CosmosObject>(
                Encoding.UTF8.GetBytes("  {\"a\": [1, 2, 3]}  "));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(3, ((CosmosArray)result.Result["a"]).Count);
        }

        private sealed class Person
        {
            public static readonly string[] Names = new string[]
            {
                "Alice",
                "Bob",
                "Charlie",
                "Craig",
                "Dave",
                "Eve",
                "Faythe",
                "Grace",
                "Heidi",
                "Ivan",
                "Judy",
                "Mallory",
                "Olivia",
                "Peggy",
                "Sybil",
                "Ted",
                "Trudy",
                "Walter",
                "Wendy"
            };

            public Person(string name, int age, Person[] children)
            {
                this.Name = name;
                this.Age = age;
                this.Children = children;
            }

            public string Name
            {
                get;
            }

            public int Age
            {
                get;
            }

            public Person[] Children
            {
                get;
            }

            public static Person CreateRandomPerson(Random random)
            {
                string name = Names[random.Next() % Names.Length];
                int age = random.Next(0, 100);

                List<Person> children = new List<Person>();
                if (random.Next() % 2 == 0)
                {
                    int numChildren = random.Next(0, 3);
                    for (int i = 0; i < numChildren; i++)
                    {
                        children.Add(CreateRandomPerson(random));
                    }
                }

                return new Person(name, age, children.ToArray());
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Person person))
                {
                    return false;
                }

                return (this.Age == person.Age)
                    && (this.Name == person.Name)
                    && (this.Children.Length == person.Children.Length)
                    && this.Children.SequenceEqual(person.Children);
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

        private class LazilyDeserializedPerson
        {
            private readonly CosmosObject cosmosObject;

            public LazilyDeserializedPerson(CosmosObject cosmosObject)
            {
                this.cosmosObject = cosmosObject;
            }

            public string Name => (this.cosmosObject[nameof(Person.Name)] as CosmosString).Value;

            public int Age
            {
                get
                {
                    CosmosNumber cosmosNumber = this.cosmosObject[nameof(Person.Age)] as CosmosNumber;
                    int age = (int)Number64.ToLong(cosmosNumber.Value);
                    return age;
                }
            }

            public IEnumerable<LazilyDeserializedPerson> Children
            {
                get
                {
                    CosmosArray children = this.cosmosObject[nameof(Person.Children)] as CosmosArray;
                    foreach (CosmosElement child in children)
                    {
                        yield return new LazilyDeserializedPerson(child as CosmosObject);
                    }
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestQuickNavigation()
        {
            CosmosArray lazilyDeserializedPeople = CosmosElement.CreateFromBuffer<CosmosArray>(LazyCosmosElementTests.bufferedSerializedPeople);
            LazilyDeserializedPerson lazilyDeserializedFirstPerson = new LazilyDeserializedPerson(lazilyDeserializedPeople[0] as CosmosObject);

            Assert.AreEqual(people.First().Name, lazilyDeserializedFirstPerson.Name);
            Assert.AreEqual(people.First().Age, lazilyDeserializedFirstPerson.Age);
        }

        [TestMethod]
        [Owner("brchon")]
        public void WriteToWriter()
        {
            CosmosArray lazilyDeserializedPeople = CosmosElement.CreateFromBuffer<CosmosArray>(LazyCosmosElementTests.bufferedSerializedPeople);
            IJsonWriter jsonWriter = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text);
            lazilyDeserializedPeople.WriteTo(jsonWriter);
            byte[] bufferedResult = jsonWriter.GetResult().ToArray();

            string bufferedSerializedPeopleString = Encoding.UTF8.GetString(bufferedSerializedPeople);
            string bufferedResultString = Encoding.UTF8.GetString(bufferedResult);
            Assert.AreEqual(bufferedSerializedPeopleString, bufferedResultString);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Materialize()
        {
            CosmosArray lazilyDeserializedPeople = CosmosElement.CreateFromBuffer<CosmosArray>(LazyCosmosElementTests.bufferedSerializedPeople);
            IReadOnlyList<Person> materialziedPeople = lazilyDeserializedPeople.Materialize<IReadOnlyList<Person>>();
            Assert.IsTrue(people.SequenceEqual(materialziedPeople));
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestCaching()
        {
            CosmosArray lazilyDeserializedPeople = CosmosElement.CreateFromBuffer<CosmosArray>(LazyCosmosElementTests.bufferedSerializedPeople);
            Assert.IsTrue(
                object.ReferenceEquals(lazilyDeserializedPeople[0], lazilyDeserializedPeople[0]),
                "Array did not return the item from the cache.");

            CosmosObject lazilyDeserializedPerson = lazilyDeserializedPeople[0] as CosmosObject;
            foreach (string key in lazilyDeserializedPerson.Keys)
            {
                Assert.IsTrue(
                    object.ReferenceEquals(lazilyDeserializedPerson[key], lazilyDeserializedPerson[key]),
                    "Object property was not served from the cache");
            }

            foreach ((string key1, string key2) in lazilyDeserializedPerson.Keys.Zip(lazilyDeserializedPerson.Keys, (first, second) => (first, second)))
            {
                Assert.IsTrue(
                    object.ReferenceEquals(key1, key2),
                    "Object keys are not served from the cache.");
            }

            foreach ((CosmosElement value1, CosmosElement value2) in lazilyDeserializedPerson.Values.Zip(lazilyDeserializedPerson.Values, (first, second) => (first, second)))
            {
                Assert.IsTrue(
                    object.ReferenceEquals(value1, value2),
                    "Object values are not served from the cache.");
            }

            int countFromCount = lazilyDeserializedPerson.Count;
            int countFromEnumerator = 0;
            foreach (KeyValuePair<string, CosmosElement> kvp in lazilyDeserializedPerson)
            {
                countFromEnumerator++;
            }

            Assert.AreEqual(countFromCount, countFromEnumerator);

            // Can not test for strings, since UtfAnyString allocates a light weight wrapper.

            int i = 0;
            foreach (CosmosElement arrayItem in lazilyDeserializedPeople)
            {
                Assert.IsTrue(
                    object.ReferenceEquals(arrayItem, lazilyDeserializedPeople[i++]));
            }

            Assert.AreEqual(i, lazilyDeserializedPeople.Count);

            int count = lazilyDeserializedPeople.Count;
            for (i = 0; i < count; i++)
            {
                Assert.IsTrue(
                    object.ReferenceEquals(lazilyDeserializedPeople[i], lazilyDeserializedPeople[i]));
            }

            // Numbers is a value type so we don't need to test for the cache.
            // Booleans are multitons so we don't need to test for the cache.
            // Nulls are singletons so we don't need to test for the cache.
        }

        #region CurratedDocs
        [TestMethod]
        [Owner("brchon")]
        public void CombinedScriptsDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void DevTestCollTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("devtestcoll.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LastFMTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("lastfm");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LogDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("LogData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MillionSong1KDocumentsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MsnCollectionTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("MsnCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void NutritionDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("NutritionData");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("runsCollection");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesCommitteesTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("states_committees.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesLegislatorsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("states_legislators");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Store01Test()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("store01C.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TicinoErrorBucketsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("TicinoErrorBuckets");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("twitter_data");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("ups1");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromFile("XpertEvents");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Null()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("null");
        }

        [TestMethod]
        [Owner("brchon")]
        public void False()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("false");
        }

        [TestMethod]
        [Owner("brchon")]
        public void True()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("true");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Number()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("123");
        }

        [TestMethod]
        [Owner("brchon")]
        public void String()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("\"hello\"");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Array()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("[1, 2, 3]");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Object()
        {
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson("{\"foo\" : \"bar\"}");
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllTypes()
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
            LazyCosmosElementTests.TestCosmosElementVisitabilityFromJson(input);
        }

        private static void TestCosmosElementVisitabilityFromFile(string filename)
        {
            string json = SampleJsonFromFile(filename);
            TestCosmosElementVisitabilityFromJson(json);
        }

        private static void TestCosmosElementVisitabilityFromJson(string json)
        {
            ReadOnlyMemory<byte> payload = LazyCosmosElementTests.ConvertStringToBinary(json);

            CosmosElement cosmosElement = CosmosElement.CreateFromBuffer(payload);

            IJsonWriter jsonWriterIndexer = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            IJsonWriter jsonWriterEnumerable = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);

            LazyCosmosElementTests.VisitCosmosElementIndexer(cosmosElement, jsonWriterIndexer);
            LazyCosmosElementTests.VisitCosmosElementEnumerable(cosmosElement, jsonWriterEnumerable);

            CosmosElement cosmosElementFromIndexer = CosmosElement.CreateFromBuffer(jsonWriterIndexer.GetResult());
            CosmosElement cosmosElementFromEnumerable = CosmosElement.CreateFromBuffer(jsonWriterEnumerable.GetResult());

            Assert.AreEqual(cosmosElement, cosmosElementFromIndexer);
            Assert.AreEqual(cosmosElement, cosmosElementFromEnumerable);
        }

        private static string SampleJsonFromFile(string filename)
        {
            string path = string.Format("TestJsons/{0}", filename);
            string json = TextFileConcatenation.ReadMultipartFile(path);

            IEnumerable<object> documents = null;
            try
            {
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
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to get JSON payload: {json[..128]} {ex}");
            }

            documents = documents.OrderBy(x => Guid.NewGuid()).Take(100);

            json = JsonConvert.SerializeObject(documents);
            return json;
        }

        private static ReadOnlyMemory<byte> ConvertStringToBinary(string json)
        {
            IJsonReader jsonReader = Microsoft.Azure.Cosmos.Json.JsonReader.Create(Encoding.UTF8.GetBytes(json));
            IJsonWriter jsonWriter = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonReader.WriteAll(jsonWriter);
            return jsonWriter.GetResult();
        }

        private static void VisitCosmosElementIndexer(CosmosElement cosmosElement, IJsonWriter jsonWriter)
        {
            switch (cosmosElement)
            {
                case CosmosString cosmosString:
                    LazyCosmosElementTests.VisitCosmosString(cosmosString, jsonWriter);
                    break;
                case CosmosNumber cosmosNumber:
                    LazyCosmosElementTests.VisitCosmosNumber(cosmosNumber, jsonWriter);
                    break;
                case CosmosObject cosmosObject:
                    LazyCosmosElementTests.VisitCosmosObjectIndexer(cosmosObject, jsonWriter);
                    break;
                case CosmosArray cosmosArray:
                    LazyCosmosElementTests.VisitCosmosArrayIndexer(cosmosArray, jsonWriter);
                    break;
                case CosmosBoolean cosmosBoolean:
                    LazyCosmosElementTests.VisitCosmosBoolean(cosmosBoolean, jsonWriter);
                    break;
                case CosmosNull cosmosNull:
                    LazyCosmosElementTests.VisitCosmosNull(cosmosNull, jsonWriter);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static void VisitCosmosElementEnumerable(CosmosElement cosmosElement, IJsonWriter jsonWriter)
        {
            switch (cosmosElement)
            {
                case CosmosString cosmosString:
                    LazyCosmosElementTests.VisitCosmosString(cosmosString, jsonWriter);
                    break;
                case CosmosNumber cosmosNumber:
                    LazyCosmosElementTests.VisitCosmosNumber(cosmosNumber, jsonWriter);
                    break;
                case CosmosObject cosmosObject:
                    LazyCosmosElementTests.VisitCosmosObjectEnumerable(cosmosObject, jsonWriter);
                    break;
                case CosmosArray cosmosArray:
                    LazyCosmosElementTests.VisitCosmosArrayEnumerable(cosmosArray, jsonWriter);
                    break;
                case CosmosBoolean cosmosBoolean:
                    LazyCosmosElementTests.VisitCosmosBoolean(cosmosBoolean, jsonWriter);
                    break;
                case CosmosNull cosmosNull:
                    LazyCosmosElementTests.VisitCosmosNull(cosmosNull, jsonWriter);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static void VisitCosmosString(CosmosString cosmosString, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteStringValue(cosmosString.Value);
        }

        private static void VisitCosmosNumber(CosmosNumber cosmosNumber, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteNumberValue(cosmosNumber.Value);
        }

        private static void VisitCosmosObjectIndexer(CosmosObject cosmosObject, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteObjectStart();

            foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
            {
                jsonWriter.WriteFieldName(kvp.Key);
                LazyCosmosElementTests.VisitCosmosElementIndexer(cosmosObject[kvp.Key], jsonWriter);
            }

            jsonWriter.WriteObjectEnd();
        }

        private static void VisitCosmosObjectEnumerable(CosmosObject cosmosObject, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteObjectStart();

            foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
            {
                jsonWriter.WriteFieldName(kvp.Key);
                LazyCosmosElementTests.VisitCosmosElementIndexer(kvp.Value, jsonWriter);
            }

            jsonWriter.WriteObjectEnd();
        }

        private static void VisitCosmosArrayIndexer(CosmosArray cosmosArray, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteArrayStart();

            for (int i = 0; i < cosmosArray.Count; i++)
            {
                CosmosElement arrayItem = cosmosArray[i];
                LazyCosmosElementTests.VisitCosmosElementIndexer(arrayItem, jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
        }

        private static void VisitCosmosArrayEnumerable(CosmosArray cosmosArray, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteArrayStart();

            foreach (CosmosElement arrayItem in cosmosArray)
            {
                LazyCosmosElementTests.VisitCosmosElementIndexer(arrayItem, jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
        }

        private static void VisitCosmosBoolean(CosmosBoolean cosmosBoolean, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteBoolValue(cosmosBoolean.Value);
        }

        private static void VisitCosmosNull(CosmosNull cosmosNull, IJsonWriter jsonWriter)
        {
            jsonWriter.WriteNullValue();
        }

        private static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
        #endregion
    }
}