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
    /// Converter used to support System.Text.Json de/serialization of type Point/>.
    /// </summary>
    internal class PointSTJConverter : JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            Position pos = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;

            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Position))
                {
                    pos = JsonSerializer.Deserialize<Position>(property.Value.GetRawText(), options);
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
            return new Point(pos, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, Point point, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            
            writer.WriteStartObject(STJMetaDataFields.Position);
            JsonSerializer.Serialize(writer, point.Position, options);
            writer.WriteEndObject();

            SpatialHelper.SerializePartialSpatialObject(point.Crs, (int)point.Type, point.BoundingBox, point.AdditionalProperties, writer, options);
            writer.WriteEndObject();

        }
    }
}
