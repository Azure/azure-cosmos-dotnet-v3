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

            writer.WriteStartArray();
            foreach (double coordinate in position.Coordinates)
            {
                writer.WriteNumberValue(coordinate);
            }

            writer.WriteEndArray();
        }

        public static Position ReadProperty(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Null)
            {
                throw new JsonException(RMResources.SpatialInvalidPosition);
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(RMResources.SpatialInvalidPosition);
            }

            List<double> coordinates = new List<double>(root.GetArrayLength());
            foreach (JsonElement item in root.EnumerateArray())
            {
                if (!item.TryGetDouble(out double coordinate))
                {
                    throw new JsonException(RMResources.SpatialInvalidPosition);
                }

                coordinates.Add(coordinate);
            }

            return new Position(coordinates);
        }
    }
}
