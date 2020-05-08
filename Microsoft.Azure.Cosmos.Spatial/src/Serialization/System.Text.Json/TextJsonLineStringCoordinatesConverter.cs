//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

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

            writer.WriteStartArray();
            foreach (Position position in lineStringCoordinates.Positions)
            {
                TextJsonPositionConverter.WritePropertyValues(writer, position, options);
            }

            writer.WriteEndArray();
        }

        public static LineStringCoordinates ReadProperty(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(RMResources.SpatialInvalidPosition);
            }

            List<Position> positions = new List<Position>();
            foreach (JsonElement jsonElement in root.EnumerateArray())
            {
                positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
            }

            return new LineStringCoordinates(positions);
        }
    }
}
