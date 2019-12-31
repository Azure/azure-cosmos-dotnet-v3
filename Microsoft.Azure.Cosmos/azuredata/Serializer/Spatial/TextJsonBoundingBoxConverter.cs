//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal sealed class TextJsonBoundingBoxConverter : JsonConverter<BoundingBox>
    {
        public override BoundingBox Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonBoundingBoxConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            BoundingBox boundingBox,
            JsonSerializerOptions options)
        {
            TextJsonBoundingBoxConverter.WritePropertyValues(writer, boundingBox, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            BoundingBox boundingBox,
            JsonSerializerOptions options)
        {
            if (boundingBox == null)
            {
                return;
            }

            writer.WriteStartObject();

            if (boundingBox.Min != null)
            {
                writer.WritePropertyName("min");
                TextJsonPositionConverter.WritePropertyValues(writer, boundingBox.Min, options);
            }

            if (boundingBox.Max != null)
            {
                writer.WritePropertyName("max");
                TextJsonPositionConverter.WritePropertyValues(writer, boundingBox.Max, options);
            }

            writer.WriteEndObject();
        }

        public static BoundingBox ReadProperty(JsonElement root)
        {
            Position min = null;
            if (root.TryGetProperty("min", out JsonElement minElement))
            {
                min = TextJsonPositionConverter.ReadProperty(minElement);
            }

            Position max = null;
            if (root.TryGetProperty("max", out JsonElement maxElement))
            {
                max = TextJsonPositionConverter.ReadProperty(maxElement);
            }

            return new BoundingBox(min, max);
        }
    }
}
