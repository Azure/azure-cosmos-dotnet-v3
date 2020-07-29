//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal sealed class TextJsonGeometryConverter : JsonConverter<Geometry>
    {
        public override Geometry Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonGeometryConverter.ReadProperty(root, options);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Geometry geometry,
            JsonSerializerOptions options)
        {
            TextJsonGeometryConverter.WritePropertyValues(writer, geometry, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            Geometry geometry,
            JsonSerializerOptions options)
        {
            if (geometry == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(JsonEncodedStrings.Type, geometry.Type.ToString());

            if (geometry.BoundingBox != null)
            {
                writer.WritePropertyName(JsonEncodedStrings.BoundingBox);
                TextJsonBoundingBoxConverter.WritePropertyValues(writer, geometry.BoundingBox, options);
            }

            if (geometry.AdditionalProperties != null)
            {
                TextJsonObjectToPrimitiveConverter.SerializeDictionary(writer, geometry.AdditionalProperties, options);
            }

            switch (geometry.Type)
            {
                case GeometryType.Point:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    Point point = geometry as Point;
                    TextJsonPositionConverter.WritePropertyValues(writer, point.Position, options);
                    break;
                case GeometryType.MultiPoint:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    MultiPoint multiPoint = geometry as MultiPoint;
                    writer.WriteStartArray();
                    foreach (Position position in multiPoint.Points)
                    {
                        TextJsonPositionConverter.WritePropertyValues(writer, position, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeometryType.LineString:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    LineString lineString = geometry as LineString;
                    writer.WriteStartArray();
                    foreach (Position position in lineString.Positions)
                    {
                        TextJsonPositionConverter.WritePropertyValues(writer, position, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeometryType.MultiLineString:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    MultiLineString multiLineString = geometry as MultiLineString;
                    writer.WriteStartArray();
                    foreach (LineStringCoordinates lineCoordinates in multiLineString.LineStrings)
                    {
                        TextJsonLineStringCoordinatesConverter.WritePropertyValues(writer, lineCoordinates, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeometryType.Polygon:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    Polygon polygon = geometry as Polygon;
                    writer.WriteStartArray();
                    foreach (LinearRing linearRing in polygon.Rings)
                    {
                        TextJsonLinearRingConverter.WritePropertyValues(writer, linearRing, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeometryType.MultiPolygon:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    MultiPolygon multiPolygon = geometry as MultiPolygon;
                    writer.WriteStartArray();
                    foreach (PolygonCoordinates polygonCoordinates in multiPolygon.Polygons)
                    {
                        TextJsonPolygonCoordinatesConverter.WritePropertyValues(writer, polygonCoordinates, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeometryType.GeometryCollection:
                    writer.WritePropertyName(JsonEncodedStrings.Geometries);
                    GeometryCollection geometryCollection = geometry as GeometryCollection;
                    writer.WriteStartArray();
                    foreach (Geometry geometryIn in geometryCollection.Geometries)
                    {
                        TextJsonGeometryConverter.WritePropertyValues(writer, geometryIn, options);
                    }

                    writer.WriteEndArray();
                    break;
            }

            writer.WriteEndObject();
        }

        public static Geometry ReadProperty(
            JsonElement root,
            JsonSerializerOptions options)
        {
            if (root.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            if (!root.TryGetProperty(JsonEncodedStrings.Type.EncodedUtf8Bytes, out JsonElement typeElement)
                || typeElement.ValueKind != JsonValueKind.String
                || !Enum.TryParse(typeElement.GetString(), out GeometryType geometryType))
            {
                throw new JsonException(RMResources.SpatialInvalidGeometryType);
            }

            GeometryParams geometryParams = TextJsonGeometryParamsJsonConverter.ReadProperty(root, options);

            Geometry geometry = null;
            switch (geometryType)
            {
                case GeometryType.GeometryCollection:
                    {
                        List<Geometry> geometries = new List<Geometry>();
                        if (root.TryGetProperty(JsonEncodedStrings.Geometries.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                geometries.Add(TextJsonGeometryConverter.ReadProperty(jsonElement, options));
                            }
                        }

                        geometry = new GeometryCollection(geometries, geometryParams);
                    }
                    break;
                case GeometryType.LineString:
                    {
                        List<Position> positions = new List<Position>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new LineString(positions, geometryParams);
                    }
                    break;
                case GeometryType.MultiLineString:
                    {
                        List<LineStringCoordinates> lines = new List<LineStringCoordinates>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement lineElement in coordinatesElement.EnumerateArray())
                            {
                                lines.Add(TextJsonLineStringCoordinatesConverter.ReadProperty(lineElement));
                            }
                        }

                        geometry = new MultiLineString(lines, geometryParams);
                    }
                    break;
                case GeometryType.Point:
                    {
                        Position position = null;
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            position = TextJsonPositionConverter.ReadProperty(coordinatesElement);
                            geometry = new Point(position, geometryParams);
                        }

                        geometry = new Point(position, geometryParams);
                    }
                    break;
                case GeometryType.MultiPoint:
                    {
                        List<Position> positions = new List<Position>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new MultiPoint(positions, geometryParams);
                    }
                    break;
                case GeometryType.Polygon:
                    {
                        List<LinearRing> linearRings = new List<LinearRing>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                linearRings.Add(TextJsonLinearRingConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new Polygon(linearRings, geometryParams);
                    }
                    break;
                case GeometryType.MultiPolygon:
                    {
                        List<PolygonCoordinates> polygonCoordinates = new List<PolygonCoordinates>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                polygonCoordinates.Add(TextJsonPolygonCoordinatesConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new MultiPolygon(polygonCoordinates, geometryParams);
                    }
                    break;
            }

            return geometry;
        }
    }
}
