namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Tests.Poco.STJ;
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
    }
}