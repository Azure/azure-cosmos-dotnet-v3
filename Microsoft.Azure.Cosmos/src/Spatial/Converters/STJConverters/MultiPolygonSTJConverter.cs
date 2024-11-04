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
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Polygons))
                {
                    coordinates = new List<PolygonCoordinates>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        PolygonCoordinates coordinate = JsonSerializer.Deserialize<PolygonCoordinates>(arrayElement.GetRawText(), options);
                        coordinates.Add(coordinate);
                    }
                }
                else if (property.NameEquals(STJMetaDataFields.AdditionalProperties))
                {
                    additionalProperties = JsonSerializer.Deserialize<IDictionary<string, object>>(property.Value.ToString(), options);
                    Console.WriteLine(additionalProperties.ToString());
                }
                else if (property.NameEquals(STJMetaDataFields.Crs))
                {
                    crs = property.Value.ValueKind == JsonValueKind.Null
                        ? Crs.Unspecified
                        : JsonSerializer.Deserialize<Crs>(property.Value.ToString(), options);

                }
                else if (property.NameEquals(STJMetaDataFields.BoundingBox))
                {
                    boundingBox = JsonSerializer.Deserialize<BoundingBox>(property.Value.ToString(), options);

                }

            }
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
