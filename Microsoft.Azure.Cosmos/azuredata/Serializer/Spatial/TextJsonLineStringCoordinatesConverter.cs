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

    internal sealed class TextJsonLineStringCoordinatesConverter : JsonConverter<LineStringCoordinates>
    {
        public override LineStringCoordinates Read(
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
            return TextJsonLineStringCoordinatesConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            LineStringCoordinates lineStringCoordinates,
            JsonSerializerOptions options)
        {
            TextJsonLineStringCoordinatesConverter.WritePropertyValues(writer, lineStringCoordinates, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            LineStringCoordinates lineStringCoordinates,
            JsonSerializerOptions options)
        {
            if (lineStringCoordinates == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("coordinates");
            writer.WriteStartArray();
            foreach (Position position in lineStringCoordinates.Positions)
            {
                TextJsonPositionConverter.WritePropertyValues(writer, position, options);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public static LineStringCoordinates ReadProperty(JsonElement root)
        {
            List<Position> positions = new List<Position>();
            if (root.TryGetProperty("coordinates", out JsonElement coordinatesElement))
            {
                foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                {
                    positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
                }
            }

            return new LineStringCoordinates(positions);
        }
    }
}
