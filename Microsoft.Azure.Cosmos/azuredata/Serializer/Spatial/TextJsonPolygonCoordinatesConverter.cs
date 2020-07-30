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

    internal sealed class TextJsonPolygonCoordinatesConverter : JsonConverter<PolygonCoordinates>
    {
        public override PolygonCoordinates Read(
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
            return TextJsonPolygonCoordinatesConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            PolygonCoordinates polygonCoordinates,
            JsonSerializerOptions options)
        {
            TextJsonPolygonCoordinatesConverter.WritePropertyValues(writer, polygonCoordinates, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            PolygonCoordinates polygonCoordinates,
            JsonSerializerOptions options)
        {
            if (polygonCoordinates == null)
            {
                return;
            }

            writer.WriteStartArray();
            foreach (LinearRing linearRing in polygonCoordinates)
            {
                TextJsonLinearRingConverter.WritePropertyValues(writer, linearRing, options);
            }

            writer.WriteEndArray();
        }

        public static PolygonCoordinates ReadProperty(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(RMResources.SpatialInvalidPosition);
            }

            List<LinearRing> linearRings = new List<LinearRing>();
            foreach (JsonElement jsonElement in root.EnumerateArray())
            {
                linearRings.Add(TextJsonLinearRingConverter.ReadProperty(jsonElement));
            }

            return new PolygonCoordinates(linearRings);
        }
    }
}
