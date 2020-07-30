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

    internal sealed class TextJsonGeometryConverter : JsonConverter<GeoJson>
    {
        public override GeoJson Read(
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
            GeoJson geometry,
            JsonSerializerOptions options)
        {
            TextJsonGeometryConverter.WritePropertyValues(writer, geometry, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            GeoJson geometry,
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

            switch (geometry.Type)
            {
                case GeoJsonType.Point:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    Point point = geometry as Point;
                    TextJsonPositionConverter.WritePropertyValues(writer, point.Coordinates, options);
                    break;
                case GeoJsonType.MultiPoint:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    MultiPoint multiPoint = geometry as MultiPoint;
                    writer.WriteStartArray();
                    foreach (Position position in multiPoint.Coordinates)
                    {
                        TextJsonPositionConverter.WritePropertyValues(writer, position, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeoJsonType.LineString:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    LineString lineString = geometry as LineString;
                    writer.WriteStartArray();
                    foreach (Position position in lineString.Coordinates)
                    {
                        TextJsonPositionConverter.WritePropertyValues(writer, position, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeoJsonType.MultiLineString:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    MultiLineString multiLineString = geometry as MultiLineString;
                    writer.WriteStartArray();
                    foreach (LineStringCoordinates lineCoordinates in multiLineString.Coordinates)
                    {
                        TextJsonLineStringCoordinatesConverter.WritePropertyValues(writer, lineCoordinates, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeoJsonType.Polygon:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    Polygon polygon = geometry as Polygon;
                    writer.WriteStartArray();
                    foreach (LinearRing linearRing in polygon.Coordinates)
                    {
                        TextJsonLinearRingConverter.WritePropertyValues(writer, linearRing, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeoJsonType.MultiPolygon:
                    writer.WritePropertyName(JsonEncodedStrings.Coordinates);
                    MultiPolygon multiPolygon = geometry as MultiPolygon;
                    writer.WriteStartArray();
                    foreach (PolygonCoordinates polygonCoordinates in multiPolygon.Coordinates)
                    {
                        TextJsonPolygonCoordinatesConverter.WritePropertyValues(writer, polygonCoordinates, options);
                    }

                    writer.WriteEndArray();
                    break;
                case GeoJsonType.GeometryCollection:
                    writer.WritePropertyName(JsonEncodedStrings.Geometries);
                    GeometryCollection geometryCollection = geometry as GeometryCollection;
                    writer.WriteStartArray();
                    foreach (GeoJson geometryIn in geometryCollection.Geometries)
                    {
                        TextJsonGeometryConverter.WritePropertyValues(writer, geometryIn, options);
                    }

                    writer.WriteEndArray();
                    break;
            }

            writer.WriteEndObject();
        }

        public static GeoJson ReadProperty(
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
                || !Enum.TryParse(typeElement.GetString(), out GeoJsonType geometryType))
            {
                throw new JsonException(RMResources.SpatialInvalidGeometryType);
            }

            BoundingBox boundingBox = TextJsonBoundingBoxConverter.ReadProperty(root);

            GeoJson geometry = null;
            switch (geometryType)
            {
                case GeoJsonType.GeometryCollection:
                    {
                        List<GeoJson> geometries = new List<GeoJson>();
                        if (root.TryGetProperty(JsonEncodedStrings.Geometries.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                geometries.Add(TextJsonGeometryConverter.ReadProperty(jsonElement, options));
                            }
                        }

                        geometry = new GeometryCollection(geometries, boundingBox);
                    }
                    break;
                case GeoJsonType.LineString:
                    {
                        List<Position> positions = new List<Position>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new LineString(positions, boundingBox);
                    }
                    break;
                case GeoJsonType.MultiLineString:
                    {
                        List<LineStringCoordinates> lines = new List<LineStringCoordinates>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement lineElement in coordinatesElement.EnumerateArray())
                            {
                                lines.Add(TextJsonLineStringCoordinatesConverter.ReadProperty(lineElement));
                            }
                        }

                        geometry = new MultiLineString(lines, boundingBox);
                    }
                    break;
                case GeoJsonType.Point:
                    {
                        Position position = null;
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            position = TextJsonPositionConverter.ReadProperty(coordinatesElement);
                            geometry = new Point(position, boundingBox);
                        }

                        geometry = new Point(position, boundingBox);
                    }
                    break;
                case GeoJsonType.MultiPoint:
                    {
                        List<Position> positions = new List<Position>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                positions.Add(TextJsonPositionConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new MultiPoint(positions, boundingBox);
                    }
                    break;
                case GeoJsonType.Polygon:
                    {
                        List<LinearRing> linearRings = new List<LinearRing>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                linearRings.Add(TextJsonLinearRingConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new Polygon(linearRings, boundingBox);
                    }
                    break;
                case GeoJsonType.MultiPolygon:
                    {
                        List<PolygonCoordinates> polygonCoordinates = new List<PolygonCoordinates>();
                        if (root.TryGetProperty(JsonEncodedStrings.Coordinates.EncodedUtf8Bytes, out JsonElement coordinatesElement))
                        {
                            foreach (JsonElement jsonElement in coordinatesElement.EnumerateArray())
                            {
                                polygonCoordinates.Add(TextJsonPolygonCoordinatesConverter.ReadProperty(jsonElement));
                            }
                        }

                        geometry = new MultiPolygon(polygonCoordinates, boundingBox);
                    }
                    break;
            }

            return geometry;
        }
    }
}
