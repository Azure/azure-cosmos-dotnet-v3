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
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.LineStrings))
                {
                    coordinates = new List<LineStringCoordinates>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        LineStringCoordinates lineStringCoordinate = JsonSerializer.Deserialize<LineStringCoordinates>(arrayElement.GetRawText(), options);
                        coordinates.Add(lineStringCoordinate);
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
