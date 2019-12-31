//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal sealed class TextJsonLinearRingConverter : JsonConverter<LinearRing>
    {
        public override LinearRing Read(
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
            return TextJsonLinearRingConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            LinearRing linearRing,
            JsonSerializerOptions options)
        {
            TextJsonLinearRingConverter.WritePropertyValues(writer, linearRing, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            LinearRing linearRing,
            JsonSerializerOptions options)
        {
            if (linearRing == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("coordinates");
            writer.WriteStartArray();
            foreach (Position position in linearRing.Positions)
            {
                TextJsonPositionConverter.WritePropertyValues(writer, position, options);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public static LinearRing ReadProperty(JsonElement root)
        {
            List<Position> positions = new List<Position>();
            if (root.TryGetProperty("coordinates", out JsonElement coordinatesElement))
            {
                foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                {
                    positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
                }
            }

            return new LinearRing(positions);
        }
    }
}
