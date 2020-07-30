namespace Azure.Cosmos.Tests.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Azure.Cosmos.Test.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GeometryWithAdditionalPropertiesTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestPointSerialization()
        {
            string json =
                @"{
                    ""type"":""Point"",
                    ""coordinates"":[20.232323232323232,30.3],
                    ""description"":""asdf""
            }";
            PointWithAdditionalProperties point = JsonSerializer.Deserialize<PointWithAdditionalProperties>(json);

            Assert.AreEqual(2, point.Coordinates.Count);
            Assert.AreEqual(20.232323232323232, point.Coordinates.Easting);
            Assert.AreEqual(30.3, point.Coordinates.Northing);
            Assert.AreEqual("asdf", point.Description);

            string json1 = JsonSerializer.Serialize(point);
            GeoJson point1 = JsonSerializer.Deserialize<PointWithAdditionalProperties>(json1);
            Assert.AreEqual(point1, point);
        }

        internal sealed class PointWithAdditionalPropertiesJsonConverter : JsonConverter<PointWithAdditionalProperties>
        {
            public override PointWithAdditionalProperties Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                using JsonDocument json = JsonDocument.ParseValue(ref reader);
                JsonElement root = json.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("must be object");
                }

                if (!root.TryGetProperty(
                    JsonEncodedStrings.Type.EncodedUtf8Bytes,
                    out JsonElement typeElement)
                    || typeElement.ValueKind != JsonValueKind.String
                    || typeElement.GetString() != "Point")
                {
                    throw new JsonException("Invalid type");
                }

                Position position;
                if (!root.TryGetProperty(
                    JsonEncodedStrings.Coordinates.EncodedUtf8Bytes,
                    out JsonElement coordinatesElement))
                {
                    throw new JsonException("coordinates are missing");
                }

                position = TextJsonPositionConverter.ReadProperty(coordinatesElement);

                if (!root.TryGetProperty(
                    Encoding.UTF8.GetBytes("description"),
                    out JsonElement descriptionJson))
                {
                    throw new JsonException("description is missing");
                }

                return new PointWithAdditionalProperties(position, descriptionJson.GetString());
            }

            public override void Write(
                Utf8JsonWriter writer,
                PointWithAdditionalProperties value,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString(propertyName: "type", value: "Point");
                writer.WritePropertyName("coordinates");
                TextJsonPositionConverter.WritePropertyValues(writer, value.Coordinates, options);
                writer.WriteString(propertyName: "description", value: value.Description);
                writer.WriteEndObject();
            }
        }
        [JsonConverter(typeof(PointWithAdditionalPropertiesJsonConverter))]
        internal sealed class PointWithAdditionalProperties : Point, IEquatable<PointWithAdditionalProperties>
        {
            public PointWithAdditionalProperties(
                Position position,
                string description)
                : base(position)
            {
                this.Description = description;
            }

            [DataMember(Name = "description")]
            public string Description { get; }

            public override bool Equals(object obj) => obj is PointWithAdditionalProperties point && this.Equals(point);

            public bool Equals(PointWithAdditionalProperties other) => base.Equals(other) && (this.Description == other.Description);

            public override int GetHashCode() => (base.GetHashCode() * 397) ^ this.Description.GetHashCode();
        }
    }
}
