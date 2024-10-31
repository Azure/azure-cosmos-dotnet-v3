// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    internal class MultiLineStringSTJConverter : JsonConverter<MultiLineString>
    {
        public override MultiLineString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
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
                if (property.NameEquals("lineStrings"))
                {
                    coordinates = new List<LineStringCoordinates>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        LineStringCoordinates lineStringCoordinate = System.Text.Json.JsonSerializer.Deserialize<LineStringCoordinates>(arrayElement.GetRawText(), options);
                        coordinates.Add(lineStringCoordinate);
                    }
                }
                else if (property.NameEquals("additionalProperties"))
                {
                    additionalProperties = System.Text.Json.JsonSerializer.Deserialize<IDictionary<string, object>>(property.Value.ToString(), options);
                    Console.WriteLine(additionalProperties.ToString());
                }
                else if (property.NameEquals("crs"))
                {
                    crs = property.Value.ValueKind == JsonValueKind.Null
                        ? Crs.Unspecified
                        : System.Text.Json.JsonSerializer.Deserialize<Crs>(property.Value.ToString(), options);

                }
                else if (property.NameEquals("boundingBox"))
                {
                    boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(property.Value.ToString(), options);

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
            if (multiLineString == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteStartArray("lineStrings");
            foreach (LineStringCoordinates coordinates in multiLineString.LineStrings)
            {
                writer.WriteStartObject();
                System.Text.Json.JsonSerializer.Serialize(writer, coordinates, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            System.Text.Json.JsonSerializer.Serialize(writer, multiLineString.Crs, options);
            writer.WriteNumber("type", (int)multiLineString.Type);
            if (multiLineString.BoundingBox != null)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, multiLineString.BoundingBox, options);
            }
            if (multiLineString.AdditionalProperties.Count > 0)
            {
                writer.WritePropertyName("additionalProperties");
                System.Text.Json.JsonSerializer.Serialize(writer, multiLineString.AdditionalProperties, options);

            }

            writer.WriteEndObject();
        }

    }

}
