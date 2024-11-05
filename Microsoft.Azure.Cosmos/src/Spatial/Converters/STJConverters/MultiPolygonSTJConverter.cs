// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type MultiPolygon/>.
    /// </summary>
    internal class MultiPolygonSTJConverter : JsonConverter<MultiPolygon>
    {
        public override MultiPolygon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            IList<PolygonCoordinates> coordinates = null;
            if (rootElement.TryGetProperty(STJMetaDataFields.Polygons, out JsonElement value))
            {
                coordinates = new List<PolygonCoordinates>();
                foreach (JsonElement arrayElement in value.EnumerateArray())
                {
                    PolygonCoordinates coordinate = JsonSerializer.Deserialize<PolygonCoordinates>(arrayElement.GetRawText(), options);
                    coordinates.Add(coordinate);
                }
            }

            (IDictionary<string, object> additionalProperties, Crs crs, BoundingBox boundingBox) = SpatialHelper.DeSerializePartialSpatialObject(rootElement, options);
            return new MultiPolygon(coordinates, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, MultiPolygon multiPolygon, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteStartArray(STJMetaDataFields.Polygons);
            foreach (PolygonCoordinates coordinates in multiPolygon.Polygons)
            {
                writer.WriteStartObject();
                JsonSerializer.Serialize(writer, coordinates, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            SpatialHelper.SerializePartialSpatialObject(multiPolygon.Crs, (int)multiPolygon.Type, multiPolygon.BoundingBox, multiPolygon.AdditionalProperties, writer, options);
            writer.WriteEndObject();
        }
    }

}
