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
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Rings))
                {
                    linearRings = new List<LinearRing>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        LinearRing linearRing = JsonSerializer.Deserialize<LinearRing>(arrayElement.GetRawText(), options);
                        linearRings.Add(linearRing);
                    }
                }
                else if (property.NameEquals(STJMetaDataFields.AdditionalProperties))
                {
                    additionalProperties = JsonSerializer.Deserialize<IDictionary<string, object>>(property.Value.GetRawText(), options);
                }
                else if (property.NameEquals(STJMetaDataFields.Crs))
                {
                    crs = property.Value.ValueKind == JsonValueKind.Null
                        ? Crs.Unspecified
                        : JsonSerializer.Deserialize<Crs>(property.Value.GetRawText(), options);

                }
                else if (property.NameEquals(STJMetaDataFields.BoundingBox))
                {
                    boundingBox = JsonSerializer.Deserialize<BoundingBox>(property.Value.GetRawText(), options);

                }

            }
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
