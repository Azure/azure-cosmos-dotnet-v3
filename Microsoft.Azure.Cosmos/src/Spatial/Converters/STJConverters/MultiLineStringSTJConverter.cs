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
    /// Converter used to support System.Text.Json de/serialization of type MultiLineString/>.
    /// </summary>
    internal class MultiLineStringSTJConverter : JsonConverter<MultiLineString>
    {
        public override MultiLineString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            IList<LineStringCoordinates> coordinates = null;

            if (rootElement.TryGetProperty(STJMetaDataFields.LineStrings, out JsonElement value))
            {
                coordinates = new List<LineStringCoordinates>();
                foreach (JsonElement arrayElement in value.EnumerateArray())
                {
                    LineStringCoordinates lineStringCoordinate = JsonSerializer.Deserialize<LineStringCoordinates>(arrayElement.GetRawText(), options);
                    coordinates.Add(lineStringCoordinate);
                }
            }

            (IDictionary<string, object> additionalProperties, Crs crs, BoundingBox boundingBox) = SpatialHelper.DeSerializePartialSpatialObject(rootElement, options);

            return new MultiLineString(coordinates, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });
            
        }
        public override void Write(Utf8JsonWriter writer, MultiLineString multiLineString, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteStartArray(STJMetaDataFields.LineStrings);
            foreach (LineStringCoordinates coordinates in multiLineString.LineStrings)
            {
                writer.WriteStartObject();
                JsonSerializer.Serialize(writer, coordinates, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            SpatialHelper.SerializePartialSpatialObject(multiLineString.Crs, (int)multiLineString.Type, multiLineString.BoundingBox, multiLineString.AdditionalProperties, writer, options);

            writer.WriteEndObject();
        }

    }

}
