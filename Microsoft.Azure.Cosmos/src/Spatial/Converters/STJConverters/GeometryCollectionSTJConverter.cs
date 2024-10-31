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
    using Point = Point;

    internal class GeometryCollectionSTJConverter : JsonConverter<GeometryCollection>
    {
        public override GeometryCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            IList<Geometry> geometries = null;
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("geometries"))
                {
                    geometries = new List<Geometry>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        Geometry geometry = System.Text.Json.JsonSerializer.Deserialize<Geometry>(arrayElement.GetRawText(), options);
                        geometries.Add(geometry);
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
            return new GeometryCollection(geometries, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, GeometryCollection geometryCollection, JsonSerializerOptions options)
        {
            if (geometryCollection == null)
            {
                return;
            }
            writer.WriteStartObject();
            writer.WriteStartArray("geometries");
            foreach (Geometry geometry in geometryCollection.Geometries)
            {
                if (geometry.GetType() == typeof(Point))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (Point)geometry, options);
                }
                else if (geometry.GetType() == typeof(MultiPoint))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (MultiPoint)geometry, options);
                }
                else if (geometry.GetType() == typeof(LineString))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (LineString)geometry, options);
                }
                else if (geometry.GetType() == typeof(MultiLineString))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (MultiLineString)geometry, options);
                }
                else if (geometry.GetType() == typeof(Polygon))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (Polygon)geometry, options);
                }
                else if (geometry.GetType() == typeof(MultiPolygon))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (MultiPolygon)geometry, options);
                }
                else if (geometry.GetType() == typeof(GeometryCollection))
                {
                    System.Text.Json.JsonSerializer.Serialize(writer, (GeometryCollection)geometry, options);
                }
             
            }

            writer.WriteEndArray();
            System.Text.Json.JsonSerializer.Serialize(writer, geometryCollection.Crs, options);
            writer.WriteNumber("type", (int)geometryCollection.Type);
            if (geometryCollection.BoundingBox != null)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, geometryCollection.BoundingBox, options);
            }
            if (geometryCollection.AdditionalProperties.Count > 0)
            {
                writer.WritePropertyName("additionalProperties");
                System.Text.Json.JsonSerializer.Serialize(writer, geometryCollection.AdditionalProperties, options);

            }

            writer.WriteEndObject();

        }

    }
}
