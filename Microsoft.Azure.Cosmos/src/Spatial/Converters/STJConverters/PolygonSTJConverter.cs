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
    /// Converter used to support System.Text.Json de/serialization of type Polygon/>.
    /// </summary>
    internal class PolygonSTJConverter : JsonConverter<Polygon>
    {
        public override Polygon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            IList<LinearRing> linearRings = null;

            if (rootElement.TryGetProperty(STJMetaDataFields.Rings, out JsonElement value))
            {
                linearRings = new List<LinearRing>();
                foreach (JsonElement arrayElement in value.EnumerateArray())
                {
                    LinearRing linearRing = JsonSerializer.Deserialize<LinearRing>(arrayElement.GetRawText(), options);
                    linearRings.Add(linearRing);
                }
            }

            (IDictionary<string, object> additionalProperties, Crs crs, BoundingBox boundingBox) = SpatialHelper.DeSerializePartialSpatialObject(rootElement, options);
            return new Polygon(linearRings, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, Polygon polygon, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteStartArray(STJMetaDataFields.Rings);
            foreach (LinearRing linearRing in polygon.Rings)
            {
                writer.WriteStartObject();
                JsonSerializer.Serialize(writer, linearRing, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            SpatialHelper.SerializePartialSpatialObject(polygon.Crs, (int)polygon.Type, polygon.BoundingBox, polygon.AdditionalProperties, writer, options);
            writer.WriteEndObject();
        }

    }
}
