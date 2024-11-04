// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type LineString/>.
    /// </summary>
    internal class LineStringSTJConverter : JsonConverter<LineString>
    {
        public override LineString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            IList<Position> positions = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Positions))
                {
                    positions = new List<Position>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        Position pos = JsonSerializer.Deserialize<Position>(arrayElement.GetRawText(), options);
                        positions.Add(pos);
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
            return new LineString(positions, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });
        }
        public override void Write(Utf8JsonWriter writer, LineString lineString, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteStartArray(STJMetaDataFields.Positions);
            foreach (Position position in lineString.Positions)
            {
                writer.WriteStartObject();
                JsonSerializer.Serialize(writer, position, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            SpatialHelper.SerializePartialSpatialObject(lineString.Crs, (int)lineString.Type, lineString.BoundingBox, lineString.AdditionalProperties, writer, options);

            writer.WriteEndObject();
        }

    }

}
