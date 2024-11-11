//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DeserializerTests
    {
        [TestMethod]
        public void ArrayTest()
        {
            {
                // Schemaless array
                IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                object[] arrayValue = new object[] { (Number64)1, (Number64)2, (Number64)3, (Number64)4 };
                jsonWriter.WriteArrayStart();
                jsonWriter.WriteNumber64Value(1);
                jsonWriter.WriteNumber64Value(2);
                jsonWriter.WriteNumber64Value(3);
                jsonWriter.WriteNumber64Value(4);
                jsonWriter.WriteArrayEnd();
                ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

                {
                    // positive
                    TryCatch<IReadOnlyList<object>> tryDeserialize = JsonSerializer.Monadic.Deserialize<IReadOnlyList<object>>(buffer);
                    Assert.IsTrue(tryDeserialize.Succeeded);
                    Assert.IsTrue(tryDeserialize.Result.SequenceEqual(arrayValue));
                }

                {
                    // negative
                    TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                    Assert.IsFalse(tryDeserialize.Succeeded);
                }
            }

            {
                // Array with schema
                IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                Person[] arrayValue = new Person[] { new Person("John", 24) };
                jsonWriter.WriteArrayStart();
                jsonWriter.WriteObjectStart();
                jsonWriter.WriteFieldName("name");
                jsonWriter.WriteStringValue("John");
                jsonWriter.WriteFieldName("age");
                jsonWriter.WriteNumber64Value(24);
                jsonWriter.WriteObjectEnd();
                jsonWriter.WriteArrayEnd();
                ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

                TryCatch<IReadOnlyList<Person>> tryDeserialize = JsonSerializer.Monadic.Deserialize<IReadOnlyList<Person>>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.IsTrue(tryDeserialize.Result.SequenceEqual(arrayValue));
            }
        }

        [TestMethod]
        public void BinaryTest()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            byte[] binaryValue = new byte[] { 1, 2, 3 };
            jsonWriter.WriteBinaryValue(binaryValue);
            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            {
                // positive
                TryCatch<ReadOnlyMemory<byte>> tryDeserialize = JsonSerializer.Monadic.Deserialize<ReadOnlyMemory<byte>>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.IsTrue(tryDeserialize.Result.ToArray().SequenceEqual(binaryValue));
            }

            {
                // negative
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }
        }

        [TestMethod]
        public void BooleanTest()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonWriter.WriteBoolValue(true);
            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            {
                // positive
                TryCatch<bool> tryDeserialize = JsonSerializer.Monadic.Deserialize<bool>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual(true, tryDeserialize.Result);
            }

            {
                // negative
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }
        }

        [TestMethod]
        public void GuidTest()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonWriter.WriteGuidValue(Guid.Empty);
            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            {
                // positive
                TryCatch<Guid> tryDeserialize = JsonSerializer.Monadic.Deserialize<Guid>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual(Guid.Empty, tryDeserialize.Result);
            }

            {
                // negative
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }
        }

        [TestMethod]
        public void NullTest()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonWriter.WriteNullValue();
            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            {
                // object
                TryCatch<CosmosClient> tryDeserialize = JsonSerializer.Monadic.Deserialize<CosmosClient>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual(null, tryDeserialize.Result);
            }

            {
                // nullable
                TryCatch<int?> tryDeserialize = JsonSerializer.Monadic.Deserialize<int?>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual(null, tryDeserialize.Result);
            }

            {
                // struct
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }
        }

        [TestMethod]
        public void NumberTest()
        {
            int integerValue = 42;
            IJsonWriter integerWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            integerWriter.WriteNumber64Value(integerValue);
            ReadOnlyMemory<byte> integerBuffer = integerWriter.GetResult();

            {
                TryCatch<sbyte> tryDeserialize = JsonSerializer.Monadic.Deserialize<sbyte>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<byte> tryDeserialize = JsonSerializer.Monadic.Deserialize<byte>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<short> tryDeserialize = JsonSerializer.Monadic.Deserialize<short>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<long> tryDeserialize = JsonSerializer.Monadic.Deserialize<long>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<ushort> tryDeserialize = JsonSerializer.Monadic.Deserialize<ushort>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<uint> tryDeserialize = JsonSerializer.Monadic.Deserialize<uint>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<ulong> tryDeserialize = JsonSerializer.Monadic.Deserialize<ulong>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<decimal> tryDeserialize = JsonSerializer.Monadic.Deserialize<decimal>(integerBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((long)integerValue, (long)tryDeserialize.Result);
            }

            {
                TryCatch<float> tryDeserialize = JsonSerializer.Monadic.Deserialize<float>(integerBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<double> tryDeserialize = JsonSerializer.Monadic.Deserialize<double>(integerBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            float doubleValue = 42.1337f;
            IJsonWriter doubleWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            doubleWriter.WriteNumber64Value(doubleValue);
            ReadOnlyMemory<byte> doubleBuffer = doubleWriter.GetResult();

            {
                TryCatch<sbyte> tryDeserialize = JsonSerializer.Monadic.Deserialize<sbyte>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<byte> tryDeserialize = JsonSerializer.Monadic.Deserialize<byte>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<short> tryDeserialize = JsonSerializer.Monadic.Deserialize<short>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<long> tryDeserialize = JsonSerializer.Monadic.Deserialize<long>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<ushort> tryDeserialize = JsonSerializer.Monadic.Deserialize<ushort>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<uint> tryDeserialize = JsonSerializer.Monadic.Deserialize<uint>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<ulong> tryDeserialize = JsonSerializer.Monadic.Deserialize<ulong>(doubleBuffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }

            {
                TryCatch<decimal> tryDeserialize = JsonSerializer.Monadic.Deserialize<decimal>(doubleBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual((decimal)(double)doubleValue, tryDeserialize.Result);
            }

            {
                TryCatch<float> tryDeserialize = JsonSerializer.Monadic.Deserialize<float>(doubleBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual(doubleValue, tryDeserialize.Result);
            }

            {
                TryCatch<double> tryDeserialize = JsonSerializer.Monadic.Deserialize<double>(doubleBuffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual(doubleValue, tryDeserialize.Result);
            }
        }

        [TestMethod]
        public void ObjectTest()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonWriter.WriteObjectStart();

            jsonWriter.WriteFieldName("name");
            jsonWriter.WriteStringValue("John");

            jsonWriter.WriteFieldName("age");
            jsonWriter.WriteNumber64Value(24);

            jsonWriter.WriteObjectEnd();
            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            {
                // positive
                TryCatch<Person> tryDeserialize = JsonSerializer.Monadic.Deserialize<Person>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual("John", tryDeserialize.Result.Name);
                Assert.AreEqual(24, tryDeserialize.Result.Age);
            }

            {
                // negative
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }
        }

        [TestMethod]
        public void StringTest()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            jsonWriter.WriteStringValue("asdf");
            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();

            {
                // Positive
                TryCatch<string> tryDeserialize = JsonSerializer.Monadic.Deserialize<string>(buffer);
                Assert.IsTrue(tryDeserialize.Succeeded);
                Assert.AreEqual("asdf", tryDeserialize.Result);
            }

            {
                // Negative
                TryCatch<int> tryDeserialize = JsonSerializer.Monadic.Deserialize<int>(buffer);
                Assert.IsFalse(tryDeserialize.Succeeded);
            }
        }

        private sealed class Person
        {
            public Person(string name, int age)
            {
                this.Name = name;
                this.Age = age;
            }

            public string Name { get; }
            public int Age { get; }

            public override bool Equals(object obj)
            {
                if (!(obj is Person person))
                {
                    return false;
                }

                return this.Equals(person);
            }

            public bool Equals(Person other)
            {
                return (this.Name == other.Name) && (this.Age == other.Age);
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }
    }
}