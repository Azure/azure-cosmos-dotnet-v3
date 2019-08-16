//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.CrossPartitionQueryTests;

    [TestClass]
    public class DistinctMapTests
    {
        private static readonly Random random = new Random();

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

        public enum City
        {
            NewYork,
            LosAngeles,
            Seattle
        }

        public sealed class Pet
        {
            [JsonProperty("name")]
            public string Name { get; }

            [JsonProperty("age")]
            public int Age { get; }

            public Pet(string name, int age)
            {
                this.Name = name;
                this.Age = age;
            }
        }

        public sealed class Person
        {
            [JsonProperty("name")]
            public string Name { get; }

            [JsonProperty("city")]
            [JsonConverter(typeof(StringEnumConverter))]
            public City City { get; }

            [JsonProperty("income")]
            public double Income { get; }

            [JsonProperty("children")]
            public Person[] Children { get; }

            [JsonProperty("age")]
            public int Age { get; }

            [JsonProperty("pet")]
            public Pet Pet { get; }

            [JsonProperty("guid")]
            public Guid Guid { get; }

            public Person(string name, City city, double income, Person[] children, int age, Pet pet, Guid guid)
            {
                this.Name = name;
                this.City = city;
                this.Income = income;
                this.Children = children;
                this.Age = age;
                this.Pet = pet;
                this.Guid = guid;
            }
        }

        private static string GetRandomName(Random rand)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < rand.Next(0, 100); i++)
            {
                stringBuilder.Append('a' + rand.Next(0, 26));
            }

            return stringBuilder.ToString();
        }

        private static City GetRandomCity(Random rand)
        {
            int index = rand.Next(0, 3);
            switch (index)
            {
                case 0:
                    return City.LosAngeles;
                case 1:
                    return City.NewYork;
                case 2:
                    return City.Seattle;
            }

            return City.LosAngeles;
        }

        private static double GetRandomIncome(Random rand)
        {
            return rand.NextDouble() * double.MaxValue;
        }

        private static int GetRandomAge(Random rand)
        {
            return rand.Next();
        }

        private static Pet GetRandomPet(Random rand)
        {
            string name = DistinctMapTests.GetRandomName(rand);
            int age = DistinctMapTests.GetRandomAge(rand);
            return new Pet(name, age);
        }

        public static Person GetRandomPerson(Random rand)
        {
            string name = DistinctMapTests.GetRandomName(rand);
            City city = DistinctMapTests.GetRandomCity(rand);
            double income = DistinctMapTests.GetRandomIncome(rand);
            List<Person> people = new List<Person>();
            if (rand.Next(0, 11) % 10 == 0)
            {
                for (int i = 0; i < rand.Next(0, 5); i++)
                {
                    people.Add(DistinctMapTests.GetRandomPerson(rand));
                }
            }

            Person[] children = people.ToArray();
            int age = DistinctMapTests.GetRandomAge(rand);
            Pet pet = DistinctMapTests.GetRandomPet(rand);
            Guid guid = Guid.NewGuid();
            return new Person(name, city, income, children, age, pet, guid);
        }

        public sealed class JsonTokenEqualityComparer : IEqualityComparer<JToken>
        {
            public static JsonTokenEqualityComparer Value = new JsonTokenEqualityComparer();

            public bool Equals(double double1, double double2)
            {
                return double1 == double2;
            }

            public bool Equals(string string1, string string2)
            {
                return string1.Equals(string2);
            }

            public bool Equals(bool bool1, bool bool2)
            {
                return bool1 == bool2;
            }

            public bool Equals(JArray jArray1, JArray jArray2)
            {
                if (jArray1.Count != jArray2.Count)
                {
                    return false;
                }

                IEnumerable<Tuple<JToken, JToken>> pairwiseElements = jArray1
                    .Zip(jArray2, (first, second) => new Tuple<JToken, JToken>(first, second));
                bool deepEquals = true;
                foreach (Tuple<JToken, JToken> pairwiseElement in pairwiseElements)
                {
                    deepEquals &= this.Equals(pairwiseElement.Item1, pairwiseElement.Item2);
                }

                return deepEquals;
            }

            public bool Equals(JObject jObject1, JObject jObject2)
            {
                if (jObject1.Count != jObject2.Count)
                {
                    return false;
                }

                bool deepEquals = true;
                foreach (KeyValuePair<string, JToken> kvp in jObject1)
                {
                    string name = kvp.Key;
                    JToken value1 = kvp.Value;

                    JToken value2;
                    if (jObject2.TryGetValue(name, out value2))
                    {
                        deepEquals &= this.Equals(value1, value2);
                    }
                    else
                    {
                        return false;
                    }
                }

                return deepEquals;
            }

            public bool Equals(JToken jToken1, JToken jToken2)
            {
                if (Object.ReferenceEquals(jToken1, jToken2))
                {
                    return true;
                }

                if (jToken1 == null || jToken2 == null)
                {
                    return false;
                }

                JsonType type1 = JTokenTypeToJsonType(jToken1.Type);
                JsonType type2 = JTokenTypeToJsonType(jToken2.Type);

                // If the types don't match
                if (type1 != type2)
                {
                    return false;
                }

                switch (type1)
                {

                    case JsonType.Object:
                        return this.Equals((JObject)jToken1, (JObject)jToken2);
                    case JsonType.Array:
                        return this.Equals((JArray)jToken1, (JArray)jToken2);
                    case JsonType.Number:
                        return this.Equals((double)jToken1, (double)jToken2);
                    case JsonType.String:
                        return this.Equals(jToken1.ToString(), jToken2.ToString());
                    case JsonType.Boolean:
                        return this.Equals((bool)jToken1, (bool)jToken2);
                    case JsonType.Null:
                        return true;
                    default:
                        throw new ArgumentException();
                }
            }

            public int GetHashCode(JToken obj)
            {
                return 0;
            }

            private enum JsonType
            {
                Number,
                String,
                Null,
                Array,
                Object,
                Boolean
            }

            private static JsonType JTokenTypeToJsonType(JTokenType type)
            {
                switch (type)
                {

                    case JTokenType.Object:
                        return JsonType.Object;
                    case JTokenType.Array:
                        return JsonType.Array;
                    case JTokenType.Integer:
                    case JTokenType.Float:
                        return JsonType.Number;
                    case JTokenType.Guid:
                    case JTokenType.Uri:
                    case JTokenType.TimeSpan:
                    case JTokenType.Date:
                    case JTokenType.String:
                        return JsonType.String;
                    case JTokenType.Boolean:
                        return JsonType.Boolean;
                    case JTokenType.Null:
                        return JsonType.Null;
                    case JTokenType.None:
                    case JTokenType.Undefined:
                    case JTokenType.Constructor:
                    case JTokenType.Property:
                    case JTokenType.Comment:
                    case JTokenType.Raw:
                    case JTokenType.Bytes:
                    default:
                        throw new ArgumentException();
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosBoolean.Create(Encoding.UTF8.GetBytes("true"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosBoolean.Create(Encoding.UTF8.GetBytes("false"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosNull.Create(Encoding.UTF8.GetBytes("null"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosNumber.Create(Encoding.UTF8.GetBytes("1337"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosNumber.Create(Encoding.UTF8.GetBytes("1337.0"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            // Both of these turn into 9223372036854776000 when casted as a double causing them to not be distinct.
            CosmosElements.Add(CosmosNumber.Create(Encoding.UTF8.GetBytes("9223372036854775807")));
            CosmosElements.Add(CosmosNumber.Create(Encoding.UTF8.GetBytes("9223372036854775806")));

            // Both of these turn into the same value when casted as a float
            CosmosElements.Add(CosmosNumber.Create(Encoding.UTF8.GetBytes("1.0066367885961673E+308")));
            CosmosElements.Add(CosmosNumber.Create(Encoding.UTF8.GetBytes("8.1851780346865681E+307")));
            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosString.Create("\"Hello World\"");
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[]"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[ -2, -1, 0, 1, 2]"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[15,  22, 0.1, -7.3e-2, 77.0001e90 ]"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[true, false]"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[ null, null, null]  "));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[{}, {}]  "));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[0, 0.0, -1, -1.0, 1, 2, \"hello\", null, true, false]"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosArray.Create(Encoding.UTF8.GetBytes("[[], []]"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosObject.Create(Encoding.UTF8.GetBytes("{}"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);

            this.VerifyDistinctMap(CosmosElements);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectTest()
        {
            List<CosmosElement> CosmosElements = new List<CosmosElement>();
            CosmosElement input = CosmosObject.Create(Encoding.UTF8.GetBytes(@"{
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
                ""text"": ""tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby""}"));
            CosmosElements.Add(input);
            CosmosElements.Add(input);
            CosmosElements.Add(CosmosObject.Create(Encoding.UTF8.GetBytes("{\"pet\":{\"pet\":{\"name\":\"alice\"}},\"pet2\":{\"pet2\":{\"name\":\"alice\"}}}")));
            CosmosElements.Add(CosmosObject.Create(Encoding.UTF8.GetBytes("{\"pet\":{\"pet\":{\"name\":\"bob\"}},\"pet2\":{\"pet2\":{\"name\":\"bob\"}}}")));
            this.VerifyDistinctMap(CosmosElements);
        }

        private void VerifyDistinctMap(IEnumerable<CosmosElement> cosmosElements)
        {
            DistinctMapTests.MockDistinctMap mockDistinctMap = new DistinctMapTests.MockDistinctMap();
            DistinctMap unorderdDistinctMap = DistinctMap.Create(DistinctQueryType.Unordered, null);
            DistinctMap orderdedDistinctMap = DistinctMap.Create(DistinctQueryType.Ordered, null);
            cosmosElements = cosmosElements.OrderBy((CosmosElement) => CosmosElement.ToString());
            foreach (CosmosElement cosmosElement in cosmosElements)
            {
                UInt192? hash;
                bool addedToMock = mockDistinctMap.Add(cosmosElement, out hash);
                bool addedToUnorderd = unorderdDistinctMap.Add(cosmosElement, out hash);
                bool addedToOrderd = orderdedDistinctMap.Add(cosmosElement, out hash);

                Assert.AreEqual(
                    addedToMock,
                    addedToUnorderd,
                    $"addedToMock: {addedToMock} and addedToUnorderd: {addedToUnorderd} differ for token: {cosmosElement}");

                Assert.AreEqual(
                    addedToMock,
                    addedToOrderd,
                    $"addedToMock: {addedToMock} and addedToUnorderd: {addedToOrderd} differ for token: {cosmosElement}");
            }
        }

        private sealed class MockDistinctMap
        {
            // using custom comparer, since newtonsoft thinks this:
            // JToken.DeepEquals(JToken.Parse("8.1851780346865681E+307"), JToken.Parse("1.0066367885961673E+308"))
            // >> True
            private readonly HashSet<CosmosElement> jTokenSet = new HashSet<CosmosElement>(CosmosElementEqualityComparer.Value);

            public bool Add(CosmosElement jToken, out UInt192? hash)
            {
                hash = null;
                return this.jTokenSet.Add(jToken);
            }
        }
    }
}
