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

    internal class MultiPolygonSTJConverter : JsonConverter<MultiPolygon>
    {
        public override MultiPolygon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            IList<PolygonCoordinates> coordinates = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("polygons"))
                {
                    coordinates = new List<PolygonCoordinates>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        PolygonCoordinates coordinate = System.Text.Json.JsonSerializer.Deserialize<PolygonCoordinates>(arrayElement.GetRawText(), options);
                        coordinates.Add(coordinate);
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
            return new MultiPolygon(coordinates, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, MultiPolygon multiPolygon, JsonSerializerOptions options)
        {
            if (multiPolygon == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteStartArray("polygons");
            foreach (PolygonCoordinates coordinates in multiPolygon.Polygons)
            {
                writer.WriteStartObject();
                System.Text.Json.JsonSerializer.Serialize(writer, coordinates, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            System.Text.Json.JsonSerializer.Serialize(writer, multiPolygon.Crs, options);
            writer.WriteNumber("type", (int)multiPolygon.Type);
            if (multiPolygon.BoundingBox != null)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, multiPolygon.BoundingBox, options);
            }
            if (multiPolygon.AdditionalProperties.Count > 0)
            {
                writer.WritePropertyName("additionalProperties");
                System.Text.Json.JsonSerializer.Serialize(writer, multiPolygon.AdditionalProperties, options);

            }

            writer.WriteEndObject();
        }

    }

}
