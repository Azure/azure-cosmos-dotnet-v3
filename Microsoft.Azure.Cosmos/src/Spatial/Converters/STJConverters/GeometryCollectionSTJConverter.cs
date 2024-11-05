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
    using Point = Point;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type GeometryCollection/>.
    /// </summary>
    internal class GeometryCollectionSTJConverter : JsonConverter<GeometryCollection>
    {
        public override GeometryCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            IList<Geometry> geometries = null;
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            if (rootElement.TryGetProperty(STJMetaDataFields.Geometries, out JsonElement value))
            {
                geometries = new List<Geometry>();
                foreach (JsonElement arrayElement in value.EnumerateArray())
                {
                    GeometryShape shape = (GeometryShape)arrayElement.GetProperty("type").GetInt16();
                    Type type = shape.GetType();

                    switch (shape.ToString())
                    {
                        case nameof(GeometryShape.Point):
                            Point point = JsonSerializer.Deserialize<Point>(arrayElement.GetRawText(), options);
                            geometries.Add(point);
                            break;

                        case nameof(GeometryShape.MultiPoint):
                            MultiPoint multiPoint = JsonSerializer.Deserialize<MultiPoint>(arrayElement.GetRawText(), options);
                            geometries.Add(multiPoint);
                            break;

                        case nameof(GeometryShape.LineString):
                            LineString lineString = JsonSerializer.Deserialize<LineString>(arrayElement.GetRawText(), options);
                            geometries.Add(lineString);
                            break;

                        case nameof(GeometryShape.MultiLineString):
                            MultiLineString multiLineString = JsonSerializer.Deserialize<MultiLineString>(arrayElement.GetRawText(), options);
                            geometries.Add(multiLineString);
                            break;

                        case nameof(GeometryShape.Polygon):
                            Polygon polygon = JsonSerializer.Deserialize<Polygon>(arrayElement.GetRawText(), options);
                            geometries.Add(polygon);
                            break;

                        case nameof(GeometryShape.MultiPolygon):
                            MultiPolygon multiPolygon = JsonSerializer.Deserialize<MultiPolygon>(arrayElement.GetRawText(), options);
                            geometries.Add(multiPolygon);
                            break;

                        case nameof(GeometryShape.GeometryCollection):
                            GeometryCollection geometryCollection = JsonSerializer.Deserialize<GeometryCollection>(arrayElement.GetRawText(), options);
                            geometries.Add(geometryCollection);
                            break;

                        default:
                            throw new JsonException(RMResources.SpatialInvalidGeometryType);

                    }
                }
            }

            (IDictionary<string, object> additionalProperties, Crs crs, BoundingBox boundingBox) = SpatialHelper.DeSerializePartialSpatialObject(rootElement, options);
            return new GeometryCollection(geometries, new GeometryParams
            {
                AdditionalProperties = additionalProperties,
                BoundingBox = boundingBox,
                Crs = crs
            });

        }
        public override void Write(Utf8JsonWriter writer, GeometryCollection geometryCollection, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteStartArray(STJMetaDataFields.Geometries);
            foreach (Geometry geometry in geometryCollection.Geometries)
            {
                if (geometry.GetType() == typeof(Point))
                {
                    JsonSerializer.Serialize(writer, (Point)geometry, options);
                }
                else if (geometry.GetType() == typeof(MultiPoint))
                {
                    JsonSerializer.Serialize(writer, (MultiPoint)geometry, options);
                }
                else if (geometry.GetType() == typeof(LineString))
                {
                    JsonSerializer.Serialize(writer, (LineString)geometry, options);
                }
                else if (geometry.GetType() == typeof(MultiLineString))
                {
                    JsonSerializer.Serialize(writer, (MultiLineString)geometry, options);
                }
                else if (geometry.GetType() == typeof(Polygon))
                {
                    JsonSerializer.Serialize(writer, (Polygon)geometry, options);
                }
                else if (geometry.GetType() == typeof(MultiPolygon))
                {
                    JsonSerializer.Serialize(writer, (MultiPolygon)geometry, options);
                }
                else if (geometry.GetType() == typeof(GeometryCollection))
                {
                    JsonSerializer.Serialize(writer, (GeometryCollection)geometry, options);
                }

            }

            writer.WriteEndArray();

            SpatialHelper.SerializePartialSpatialObject(geometryCollection.Crs, (int)geometryCollection.Type, geometryCollection.BoundingBox, geometryCollection.AdditionalProperties, writer, options);
            writer.WriteEndObject();

        }

    }
}
