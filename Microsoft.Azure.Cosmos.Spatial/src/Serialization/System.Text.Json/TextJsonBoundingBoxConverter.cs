//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

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

            writer.WriteStartArray();
            foreach (double coordinate in boundingBox.Min.Coordinates.Concat(boundingBox.Max.Coordinates))
            {
                writer.WriteNumberValue(coordinate);
            }

            writer.WriteEndArray();
        }

        public static BoundingBox ReadProperty(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(RMResources.SpatialBoundingBoxInvalidCoordinates);
            }

            int coordinateCount = root.GetArrayLength();
            if (coordinateCount % 2 != 0
                || coordinateCount < 4)
            {
                throw new JsonException(RMResources.SpatialBoundingBoxInvalidCoordinates);
            }

            List<double> coordinates = new List<double>(coordinateCount);
            foreach (JsonElement coordinateElement in root.EnumerateArray())
            {
                if (!coordinateElement.TryGetDouble(out double coordinate))
                {
                    throw new JsonException(RMResources.SpatialBoundingBoxInvalidCoordinates);
                }

                coordinates.Add(coordinate);
            }

            return new BoundingBox(
                new Position(coordinates.Take(coordinateCount / 2).ToList()),
                new Position(coordinates.Skip(coordinateCount / 2).ToList()));
        }
    }
}
