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
            if (rootElement.TryGetProperty(STJMetaDataFields.Position, out JsonElement value))
            {
                pos = JsonSerializer.Deserialize<Position>(value.GetRawText(), options);
            }

            (IDictionary<string, object> additionalProperties, Crs crs, BoundingBox boundingBox) = SpatialHelper.DeSerializePartialSpatialObject(rootElement, options);

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
