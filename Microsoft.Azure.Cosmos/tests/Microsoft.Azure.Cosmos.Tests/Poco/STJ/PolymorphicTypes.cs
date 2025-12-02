namespace Microsoft.Azure.Cosmos.Tests.Poco.STJ
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // Note: [JsonPolymorphic] and [JsonDerivedType] attributes require .NET 7+.
    // Since this test project targets .NET 6, we use a custom JsonConverter approach instead.
    // The converter is registered on the base type (Shape) and writes a "$type" discriminator,
    // achieving the same polymorphic serialization behavior.

    [JsonConverter(typeof(ShapeConverter))]
    public abstract class Shape
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }
    }

    public class Circle : Shape
    {
        [JsonPropertyName("radius")]
        public double Radius { get; set; }
    }

    public class Rectangle : Shape
    {
        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }

    /// <summary>
    /// Custom converter that writes a type discriminator for polymorphic serialization.
    /// This converter is invoked when serializing through the base type (Shape),
    /// which only happens when typeof(T) is used instead of input.GetType().
    /// </summary>
    public class ShapeConverter : JsonConverter<Shape>
    {
        public override Shape Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            string shapeType = root.TryGetProperty("$type", out JsonElement typeElement)
                ? typeElement.GetString()
                : null;

            Shape result;
            if (shapeType == nameof(Circle) || root.TryGetProperty("radius", out _))
            {
                result = new Circle
                {
                    Radius = root.TryGetProperty("radius", out JsonElement radiusEl) ? radiusEl.GetDouble() : 0
                };
            }
            else if (shapeType == nameof(Rectangle) || root.TryGetProperty("width", out _))
            {
                result = new Rectangle
                {
                    Width = root.TryGetProperty("width", out JsonElement widthEl) ? widthEl.GetDouble() : 0,
                    Height = root.TryGetProperty("height", out JsonElement heightEl) ? heightEl.GetDouble() : 0
                };
            }
            else
            {
                throw new JsonException("Cannot determine shape type");
            }

            result.Id = root.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
            result.Color = root.TryGetProperty("color", out JsonElement colorEl) ? colorEl.GetString() : null;

            return result;
        }

        public override void Write(Utf8JsonWriter writer, Shape value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Write type discriminator for polymorphic deserialization
            if (value is Circle)
            {
                writer.WriteString("$type", nameof(Circle));
            }
            else if (value is Rectangle)
            {
                writer.WriteString("$type", nameof(Rectangle));
            }

            // Write base properties
            if (value.Id != null)
            {
                writer.WriteString("id", value.Id);
            }
            if (value.Color != null)
            {
                writer.WriteString("color", value.Color);
            }

            // Write derived properties
            if (value is Circle circle)
            {
                writer.WriteNumber("radius", circle.Radius);
            }
            else if (value is Rectangle rectangle)
            {
                writer.WriteNumber("width", rectangle.Width);
                writer.WriteNumber("height", rectangle.Height);
            }

            writer.WriteEndObject();
        }
    }
}
