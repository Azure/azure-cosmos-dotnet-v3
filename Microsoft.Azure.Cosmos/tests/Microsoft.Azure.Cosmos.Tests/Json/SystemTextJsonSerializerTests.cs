namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System.IO;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Tests.Poco.STJ;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for the serializer that uses <see cref="SystemTextJsonSerializer"/>.
    /// </summary>
    [TestClass]
    public sealed class SystemTextJsonSerializerTests
    {
        private SystemTextJsonSerializer systemTextJsonSerializer;

        [TestInitialize]
        public void SetUp()
        {
            this.systemTextJsonSerializer = new SystemTextJsonSerializer(new JsonSerializerOptions());
        }

        [TestMethod]
        public void TestPolymorphicSerialization_Circle_IncludesTypeDiscriminator()
        {
            // Arrange.
            Shape circle = new Circle
            {
                Id = "circle",
                Color = "Red",
                Radius = 5.0
            };

            // Act.
            Stream serializedStream = this.systemTextJsonSerializer.ToStream(circle);
            using StreamReader reader = new(serializedStream);
            string json = reader.ReadToEnd();

            // Assert.
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            JsonElement rootElement = jsonDocument.RootElement;

            Assert.AreEqual("Circle", rootElement.GetProperty("$type").GetString());
            Assert.AreEqual(5.0, rootElement.GetProperty("radius").GetDouble());
        }

        [TestMethod]
        public void TestPolymorphicSerialization_Rectangle_IncludesTypeDiscriminator()
        {
            // Arrange.
            Shape rectangle = new Rectangle
            {
                Id = "rectangle",
                Color = "Blue",
                Width = 10.0,
                Height = 20.0
            };

            // Act.
            Stream serializedStream = this.systemTextJsonSerializer.ToStream(rectangle);
            using StreamReader reader = new(serializedStream);
            string json = reader.ReadToEnd();

            // Assert.
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            JsonElement rootElement = jsonDocument.RootElement;

            Assert.AreEqual("Rectangle", rootElement.GetProperty("$type").GetString());
            Assert.AreEqual(10.0, rootElement.GetProperty("width").GetDouble());
            Assert.AreEqual(20.0, rootElement.GetProperty("height").GetDouble());
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
            Stream serializedStream = this.systemTextJsonSerializer.ToStream(original);
            Shape deserialized = this.systemTextJsonSerializer.FromStream<Shape>(serializedStream);

            // Assert.
            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOfType(deserialized, typeof(Circle));

            Circle deserializedCircle = (Circle)deserialized;
            Assert.AreEqual(original.Id, deserializedCircle.Id);
            Assert.AreEqual(original.Color, deserializedCircle.Color);
            Assert.AreEqual(((Circle)original).Radius, deserializedCircle.Radius);
        }
    }
}
