namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests.Poco.STJ;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class CosmosSystemTextJsonSerializerTest
    {
        CosmosSystemTextJsonSerializer stjSerializer;

        [TestInitialize]
        public void SetUp()
        {
            this.stjSerializer = new(
                new System.Text.Json.JsonSerializerOptions());
        }

        [TestMethod]
        public void TestPoco()
        {
            // Arrange.
            Cars car = Cars.GetRandomCar();

            // Act.
            Stream serializedStream = this.stjSerializer.ToStream<Cars>(car);
            Cars deserializedValue = this.stjSerializer.FromStream<Cars>(serializedStream);

            // Assert.
            Assert.AreEqual(car.Name, deserializedValue.Name);
            Assert.AreEqual(car.ColorCode, deserializedValue.ColorCode);
            Assert.AreEqual(car.CustomFeatures.Count, deserializedValue.CustomFeatures.Count);
        }

        [TestMethod]
        public void TestList()
        {
            // Arrange.
            List<int> list = new List<int> { 1, 2, 3 };

            // Act.
            Stream serializedStream = this.stjSerializer.ToStream<List<int>>(list);
            List<int> deserializedList = this.stjSerializer.FromStream<List<int>>(serializedStream);

            // Assert.
            Assert.AreEqual(list[0], deserializedList[0]);
            Assert.AreEqual(list[1], deserializedList[1]);
            Assert.AreEqual(list[2], deserializedList[2]);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TestBool(bool booleanValue)
        {
            // Arrange and Act.
            Stream serializedStream = this.stjSerializer.ToStream<bool>(booleanValue);
            bool deserializedValue = this.stjSerializer.FromStream<bool>(serializedStream);

            // Assert.
            if (booleanValue)
            {
                Assert.IsTrue(deserializedValue);
            }
            else
            {
                Assert.IsFalse(deserializedValue);
            }
        }

        [TestMethod]
        public void TestNumber()
        {
            {
                short value = 32;
                Stream serializedStream = this.stjSerializer.ToStream<short>(value);
                short int16Value = this.stjSerializer.FromStream<short>(serializedStream);
                Assert.AreEqual(expected: value, actual: int16Value);
            }

            {
                int value = 32;
                Stream serializedStream = this.stjSerializer.ToStream<int>(value);
                int int32Value = this.stjSerializer.FromStream<int>(serializedStream);
                Assert.AreEqual(expected: value, actual: int32Value);
            }

            {
                long value = 32;
                Stream serializedStream = this.stjSerializer.ToStream<long>(value);
                long int64Value = this.stjSerializer.FromStream<long>(serializedStream);
                Assert.AreEqual(expected: value, actual: int64Value);
            }

            {
                uint value = 32;
                Stream serializedStream = this.stjSerializer.ToStream<uint>(value);
                uint uintValue = this.stjSerializer.FromStream<uint>(serializedStream);
                Assert.AreEqual(expected: value, actual: uintValue);
            }

            {
                float value = 32.1337f;
                Stream serializedStream = this.stjSerializer.ToStream<float>(value);
                float floatValue = this.stjSerializer.FromStream<float>(serializedStream);
                Assert.AreEqual(expected: value, actual: floatValue);
            }

            {
                double value = 32.1337;
                Stream serializedStream = this.stjSerializer.ToStream<double>(value);
                double doubleValue = this.stjSerializer.FromStream<double>(serializedStream);
                Assert.AreEqual(expected: value, actual: doubleValue);
            }
        }

        [TestMethod]
        public void TestString()
        {
            // Arrange.
            string value = "asdf";

            // Act.
            Stream result = this.stjSerializer.ToStream<string>(value);
            string cosmosString = this.stjSerializer.FromStream<string>(result);

            // Assert.
            Assert.AreEqual(expected: value, actual: cosmosString.ToString());
        }

        [TestMethod]
        public void TestBinary()
        {
            // Arrange.
            byte[] value = new byte[] { 1, 2, 3 };

            // Act.
            Stream serializedStream = this.stjSerializer.ToStream<byte[]>(value);
            byte[] array = this.stjSerializer.FromStream<byte[]>(serializedStream);

            // Assert.
            Assert.IsTrue(array.SequenceEqual(value.ToArray()));
        }

        [TestMethod]
        public void TestGuid()
        {
            // Arrange.
            Guid value = Guid.NewGuid();

            // Act.
            Stream serializedStream = this.stjSerializer.ToStream<Guid>(value);
            Guid guidValue = this.stjSerializer.FromStream<Guid>(serializedStream);

            // Assert.
            Assert.AreEqual(expected: value, actual: guidValue);
        }

        [TestMethod]
        public void TestSerializeMemberName()
        {
            // Arrange.
            Cars car = Cars.GetRandomCar();

            MemberInfo[] memberInfoArray = car
                .GetType()
                .GetMembers(bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            // Act and Assert.
            foreach (MemberInfo member in memberInfoArray)
            {
                Assert.AreEqual(member.Name, this.stjSerializer.SerializeMemberName(member));
            }
        }

        [TestMethod]
        public void TestPolymorphicSerialization_IncludesTypeDiscriminator()
        {
            // Arrange.
            Shape circle = new Circle
            {
                Id = "circle",
                Color = "Red",
                Radius = 5.0
            };

            // Act.
            Stream serializedStream = this.stjSerializer.ToStream(circle);
            using StreamReader reader = new(serializedStream);
            string json = reader.ReadToEnd();

            // Assert.
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            JsonElement rootElement = jsonDocument.RootElement;

            Assert.AreEqual("Circle", rootElement.GetProperty("$type").GetString());
            Assert.AreEqual(5.0, rootElement.GetProperty("radius").GetDouble());
        }

        [TestMethod]
        public void TestPolymorphicSerialization_SerializeDeserialize_PreservesType()
        {
            // Arrange.
            Shape original = new Circle
            {
                Id = "circle",
                Color = "Green",
                Radius = 7.5
            };

            // Act.
            Stream serializedStream = this.stjSerializer.ToStream(original);
            Shape deserialized = this.stjSerializer.FromStream<Shape>(serializedStream);

            // Assert.
            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOfType(deserialized, typeof(Circle));

            Circle deserializedCircle = (Circle)deserialized;
            Assert.AreEqual(original.Id, deserializedCircle.Id);
            Assert.AreEqual(original.Color, deserializedCircle.Color);
            Assert.AreEqual(((Circle)original).Radius, deserializedCircle.Radius);
        }
        [TestMethod]
        public void TestFromStreamWithBaseStreamType()
        {
            // Arrange.
            MemoryStream memoryStream = new MemoryStream(new byte[] { 1, 2, 3 });

            // Act - FromStream<Stream> with a MemoryStream should succeed.
            Stream result = this.stjSerializer.FromStream<Stream>(memoryStream);

            // Assert.
            Assert.IsNotNull(result);
            Assert.AreSame(memoryStream, result);
        }

        [DataTestMethod]
        [DataRow(3)]      // small payload: single rented buffer, no Resize
        [DataRow(2000)]   // large payload: forces JsonMemoryWriter.Resize (pooled rent/copy/return)
        public void TestFromStreamBinaryFormat(int nameLength)
        {
            // Arrange - build a binary-encoded CloneableStream to exercise the pooled binary read path.
            BinaryRoundTripDoc original = new() { Id = "abc", Name = new string('x', nameLength), Count = 42 };
            string json;
            using (Stream textStream = this.stjSerializer.ToStream(original))
            using (StreamReader reader = new(textStream))
            {
                json = reader.ReadToEnd();
            }

            byte[] binary = JsonTestUtils.ConvertTextToBinary(json);

            // Pin the conditions that route FromStream into the pooled binary branch, so the test
            // fails loudly instead of silently falling back to the text path if they ever regress.
            Assert.AreEqual((byte)JsonSerializationFormat.Binary, binary[0]);
            Assert.IsTrue(CosmosObject.TryCreateFromBuffer(binary, out _));

            using CloneableStream binaryStream = new(
                internalStream: new MemoryStream(binary, index: 0, count: binary.Length, writable: false, publiclyVisible: true),
                allowUnsafeDataAccess: true);

            // Act.
            BinaryRoundTripDoc result = this.stjSerializer.FromStream<BinaryRoundTripDoc>(binaryStream);

            // Assert - binary path yields the same object as the text path.
            Assert.IsNotNull(result);
            Assert.AreEqual(original.Id, result.Id);
            Assert.AreEqual(original.Name, result.Name);
            Assert.AreEqual(original.Count, result.Count);
        }

        [DataTestMethod]
        [DataRow("ascii simple value")]
        [DataRow("")]
        [DataRow("quotes \" backslash \\ and slash /")]
        [DataRow("whitespace\ttab\r\nnewline")]
        [DataRow("accents: café ñ über Ångström")]
        [DataRow("cjk: 日本語 中文 한국어")]
        [DataRow("emoji surrogate pairs: \uD83D\uDE00\uD83C\uDF89\uD835\uDD4F")]
        [DataRow("control chars: \u0001\u0002\u001F end")]
        [DataRow("literal escape sequence text: \\u0041 \\n \\t")]
        public void TestStringValueRoundTripAcrossFormats(string value)
        {
            StringHolder original = new() { Value = value };
            string json = this.Serialize(original);

            Assert.AreEqual(value, this.DeserializeViaTextPath<StringHolder>(json).Value);
            Assert.AreEqual(value, this.DeserializeViaBinaryPath<StringHolder>(json).Value);
        }

        [TestMethod]
        public void TestNumericEdgeCasesAcrossFormats()
        {
            NumberHolder original = new()
            {
                MaxLong = long.MaxValue,
                MinLong = long.MinValue,
                MaxInt = int.MaxValue,
                MinInt = int.MinValue,
                MaxDouble = double.MaxValue,
                MinDouble = double.MinValue,
                NegativeFraction = -123456.789,
                SmallFraction = 0.000000123,
                Zero = 0,
                NegativeZero = -0.0,
            };
            string json = this.Serialize(original);

            foreach (NumberHolder result in new[]
            {
                this.DeserializeViaTextPath<NumberHolder>(json),
                this.DeserializeViaBinaryPath<NumberHolder>(json),
            })
            {
                Assert.AreEqual(original.MaxLong, result.MaxLong);
                Assert.AreEqual(original.MinLong, result.MinLong);
                Assert.AreEqual(original.MaxInt, result.MaxInt);
                Assert.AreEqual(original.MinInt, result.MinInt);
                Assert.AreEqual(original.MaxDouble, result.MaxDouble);
                Assert.AreEqual(original.MinDouble, result.MinDouble);
                Assert.AreEqual(original.NegativeFraction, result.NegativeFraction);
                Assert.AreEqual(original.SmallFraction, result.SmallFraction);
                Assert.AreEqual(original.Zero, result.Zero);
            }
        }

        [TestMethod]
        public void TestNullAndEmptyAcrossFormats()
        {
            ComplexDoc original = new()
            {
                NullText = null,
                EmptyText = string.Empty,
                EmptyList = new List<int>(),
                NullList = null,
                Nested = null,
            };
            string json = this.Serialize(original);

            foreach (ComplexDoc result in new[]
            {
                this.DeserializeViaTextPath<ComplexDoc>(json),
                this.DeserializeViaBinaryPath<ComplexDoc>(json),
            })
            {
                Assert.IsNull(result.NullText);
                Assert.AreEqual(string.Empty, result.EmptyText);
                Assert.IsNotNull(result.EmptyList);
                Assert.AreEqual(0, result.EmptyList.Count);
                Assert.IsNull(result.Nested);
            }
        }

        [TestMethod]
        public void TestComprehensiveRoundTripAcrossFormats()
        {
            ComplexDoc original = new()
            {
                NullText = null,
                EmptyText = string.Empty,
                Text = "mixed café 日本語 \uD83D\uDE00 \"quote\" \\slash\\",
                Flag = true,
                When = new DateTime(2026, 6, 10, 13, 45, 59, DateTimeKind.Utc),
                Identifier = Guid.Parse("3f7686c0-8cca-5292-e25a-511be5205e05"),
                Numbers = new List<int> { -1, 0, 1, int.MaxValue, int.MinValue },
                EmptyList = new List<int>(),
                Nested = new Address { City = "Seattle", Zip = "98052" },
                Addresses = new List<Address>
                {
                    new() { City = "Redmond", Zip = "98052" },
                    new() { City = "São Paulo", Zip = "01000" },
                },
            };
            string json = this.Serialize(original);

            foreach (ComplexDoc result in new[]
            {
                this.DeserializeViaTextPath<ComplexDoc>(json),
                this.DeserializeViaBinaryPath<ComplexDoc>(json),
            })
            {
                Assert.IsNull(result.NullText);
                Assert.AreEqual(original.EmptyText, result.EmptyText);
                Assert.AreEqual(original.Text, result.Text);
                Assert.AreEqual(original.Flag, result.Flag);
                Assert.AreEqual(original.When, result.When);
                Assert.AreEqual(original.Identifier, result.Identifier);
                CollectionAssert.AreEqual(original.Numbers, result.Numbers);
                Assert.AreEqual(0, result.EmptyList.Count);
                Assert.AreEqual(original.Nested.City, result.Nested.City);
                Assert.AreEqual(original.Nested.Zip, result.Nested.Zip);
                Assert.AreEqual(original.Addresses.Count, result.Addresses.Count);
                Assert.AreEqual(original.Addresses[1].City, result.Addresses[1].City);
            }
        }

        private string Serialize<T>(T value)
        {
            using Stream stream = this.stjSerializer.ToStream(value);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        // A plain (non-CloneableStream) MemoryStream routes FromStream through the text DeserializeStream path.
        private T DeserializeViaTextPath<T>(string json)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(json), writable: false);
            return this.stjSerializer.FromStream<T>(stream);
        }

        // A binary-format CloneableStream routes FromStream through the pooled binary transcode path.
        private T DeserializeViaBinaryPath<T>(string json)
        {
            byte[] binary = JsonTestUtils.ConvertTextToBinary(json);
            Assert.AreEqual((byte)JsonSerializationFormat.Binary, binary[0]);

            using CloneableStream stream = new(
                internalStream: new MemoryStream(binary, index: 0, count: binary.Length, writable: false, publiclyVisible: true),
                allowUnsafeDataAccess: true);
            return this.stjSerializer.FromStream<T>(stream);
        }

        private sealed class StringHolder
        {
            public string Value { get; set; }
        }

        private sealed class NumberHolder
        {
            public long MaxLong { get; set; }

            public long MinLong { get; set; }

            public int MaxInt { get; set; }

            public int MinInt { get; set; }

            public double MaxDouble { get; set; }

            public double MinDouble { get; set; }

            public double NegativeFraction { get; set; }

            public double SmallFraction { get; set; }

            public int Zero { get; set; }

            public double NegativeZero { get; set; }
        }

        private sealed class ComplexDoc
        {
            public string Text { get; set; }

            public string EmptyText { get; set; }

            public string NullText { get; set; }

            public bool Flag { get; set; }

            public DateTime When { get; set; }

            public Guid Identifier { get; set; }

            public List<int> Numbers { get; set; }

            public List<int> EmptyList { get; set; }

            public List<int> NullList { get; set; }

            public Address Nested { get; set; }

            public List<Address> Addresses { get; set; }
        }

        private sealed class Address
        {
            public string City { get; set; }

            public string Zip { get; set; }
        }

        private sealed class BinaryRoundTripDoc
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public int Count { get; set; }
        }
    }
}
