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

    internal sealed class TextJsonPositionConverter : JsonConverter<Position>
    {
        public override Position Read(
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
            return TextJsonPositionConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Position position,
            JsonSerializerOptions options)
        {
            TextJsonPositionConverter.WritePropertyValues(writer, position, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            Position position,
            JsonSerializerOptions options)
        {
            if (position == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("Coordinates");
            writer.WriteStartArray();
            foreach (double coordinate in position.Coordinates)
            {
                writer.WriteNumberValue(coordinate);
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public static Position ReadProperty(JsonElement root)
        {
            if (root.TryGetProperty("Coordinates", out JsonElement coordinatesElement))
            {
                List<double> coordinates = new List<double>(coordinatesElement.GetArrayLength());
                foreach (JsonElement item in coordinatesElement.EnumerateArray())
                {
                    if (item.TryGetDouble(out double coordinate))
                    {
                        coordinates.Add(coordinate);
                    }
                }

                return new Position(coordinates);
            }

            return null;
        }
    }
}
