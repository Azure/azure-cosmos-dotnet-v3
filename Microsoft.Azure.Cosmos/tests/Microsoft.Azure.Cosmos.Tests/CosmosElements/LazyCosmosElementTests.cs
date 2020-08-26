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
            Assert.IsTrue(
                object.ReferenceEquals(lazilyDeserializedPerson[nameof(Person.Age)], lazilyDeserializedPerson[nameof(Person.Age)]),
                "Object did not return the property from the cache.");

            CosmosString personName = lazilyDeserializedPerson[nameof(Person.Name)] as CosmosString;
            Assert.IsTrue(
                object.ReferenceEquals(personName.Value, personName.Value),
                "Did not return the string from the cache.");

            // Numbers is a value type so we don't need to test for the cache.
            // Booleans are multitons so we don't need to test for the cache.
            // Nulls are singletons so we don't need to test for the cache.
        }

        #region CurratedDocs
        [TestMethod]
        [Owner("brchon")]
        public void CombinedScriptsDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("CombinedScriptsData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void DevTestCollTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("devtestcoll.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LastFMTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("lastfm");
        }

        [TestMethod]
        [Owner("brchon")]
        public void LogDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("LogData.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MillionSong1KDocumentsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("MillionSong1KDocuments.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void MsnCollectionTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("MsnCollection.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void NutritionDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("NutritionData");
        }

        [TestMethod]
        [Owner("brchon")]
        public void RunsCollectionTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("runsCollection");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesCommitteesTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("states_committees.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void StatesLegislatorsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("states_legislators");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Store01Test()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("store01C.json");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TicinoErrorBucketsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("TicinoErrorBuckets");
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwitterDataTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("twitter_data");
        }

        [TestMethod]
        [Owner("brchon")]
        public void Ups1Test()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("ups1");
        }

        [TestMethod]
        [Owner("brchon")]
        public void XpertEventsTest()
        {
            LazyCosmosElementTests.TestCosmosElementVisitability("XpertEvents");
        }

        private static void TestCosmosElementVisitability(string filename)
        {
            ReadOnlyMemory<byte> payload = LazyCosmosElementTests.GetPayload(filename);

            CosmosElement cosmosElement = CosmosElement.CreateFromBuffer(payload);

            IJsonWriter jsonWriterIndexer = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            IJsonWriter jsonWriterEnumerable = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);

            LazyCosmosElementTests.VisitCosmosElementIndexer(cosmosElement, jsonWriterIndexer);
            LazyCosmosElementTests.VisitCosmosElementEnumerable(cosmosElement, jsonWriterEnumerable);

            ReadOnlySpan<byte> payloadIndexer = jsonWriterIndexer.GetResult().Span;
            ReadOnlySpan<byte> payloadEnumerable = jsonWriterEnumerable.GetResult().Span;

            Assert.IsTrue(payload.Span.SequenceEqual(payloadIndexer));
            Assert.IsTrue(payload.Span.SequenceEqual(payloadEnumerable));
        }

        private static ReadOnlyMemory<byte> GetPayload(string filename)
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
                Assert.Fail($"Failed to get JSON payload: {json.Substring(0, 128)} {ex}");
            }

            documents = documents.OrderBy(x => Guid.NewGuid()).Take(100);

            json = JsonConvert.SerializeObject(documents);

            IJsonReader jsonReader = Microsoft.Azure.Cosmos.Json.JsonReader.Create(Encoding.UTF8.GetBytes(json));
            IJsonWriter jsonWriter = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonWriter.WriteAll(jsonReader);
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
            jsonWriter.WriteNumber64Value(cosmosNumber.Value);
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
