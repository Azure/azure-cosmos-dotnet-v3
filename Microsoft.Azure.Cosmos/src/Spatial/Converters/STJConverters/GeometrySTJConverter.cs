// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A factory converter for all Geometry types including Point, LineString, Polygon, 
    /// MultiPoint, MultiLineString, MultiPolygon, and GeometryCollection.
    /// </summary>
    internal sealed class GeometrySTJConverter : JsonConverter<Geometry>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(Geometry).IsAssignableFrom(typeToConvert);
        }

        public override Geometry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            (string typeName, GeometryParams geometryParams) = this.ReadGeometryProperties(rootElement, options);
            return this.CreateGeometry(typeName, geometryParams, rootElement, options);
        }

        public override void Write(Utf8JsonWriter writer, Geometry value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("type", value.Type.ToString());
            this.WriteCoordinates(writer, value, options);
            this.WriteOptionalProperties(writer, value, options);

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the coordinates or geometries property based on the geometry type.
        /// GeometryCollection uses 'geometries' property instead of 'coordinates'.
        /// </summary>
        private void WriteCoordinates(Utf8JsonWriter writer, Geometry value, JsonSerializerOptions options)
        {
            switch (value.Type)
            {
                case GeometryType.Point:
                    writer.WritePropertyName("coordinates");
                    JsonSerializer.Serialize(writer, ((Point)value).Position, options);
                    break;
                case GeometryType.LineString:
                    writer.WritePropertyName("coordinates");
                    JsonSerializer.Serialize(writer, ((LineString)value).Positions, options);
                    break;
                case GeometryType.Polygon:
                    writer.WritePropertyName("coordinates");
                    JsonSerializer.Serialize(writer, ((Polygon)value).Rings, options);
                    break;
                case GeometryType.MultiPoint:
                    writer.WritePropertyName("coordinates");
                    JsonSerializer.Serialize(writer, ((MultiPoint)value).Points, options);
                    break;
                case GeometryType.MultiLineString:
                    writer.WritePropertyName("coordinates");
                    JsonSerializer.Serialize(writer, ((MultiLineString)value).LineStrings, options);
                    break;
                case GeometryType.MultiPolygon:
                    writer.WritePropertyName("coordinates");
                    JsonSerializer.Serialize(writer, ((MultiPolygon)value).Polygons, options);
                    break;
                case GeometryType.GeometryCollection:
                    writer.WritePropertyName("geometries");
                    JsonSerializer.Serialize(writer, ((GeometryCollection)value).Geometries, options);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value.Type), "Unsupported geometry type.");
            }
        }

        private void WriteOptionalProperties(Utf8JsonWriter writer, Geometry value, JsonSerializerOptions options)
        {
            // Match Newtonsoft Order property: Crs (2) then BoundingBox (3)
            if (value.Crs != null && !value.Crs.Equals(Crs.Default))
            {
                writer.WritePropertyName("crs");
                JsonSerializer.Serialize(writer, value.Crs, options);
            }

            if (value.BoundingBox != null)
            {
                writer.WritePropertyName("bbox");
                JsonSerializer.Serialize(writer, value.BoundingBox, options);
            }

            if (value.AdditionalProperties != null)
            {
                foreach (KeyValuePair<string, object> property in value.AdditionalProperties)
                {
                    writer.WritePropertyName(property.Key);
                    JsonSerializer.Serialize(writer, property.Value, options);
                }
            }
        }

        private (string typeName, GeometryParams geometryParams) ReadGeometryProperties(JsonElement rootElement, JsonSerializerOptions options)
        {
            if (!rootElement.TryGetProperty("type", out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Geometry object must have a 'type' property of type string.");
            }

            string typeName = typeElement.GetString();

            BoundingBox boundingBox = null;
            if (rootElement.TryGetProperty("bbox", out JsonElement bboxElement))
            {
                boundingBox = JsonSerializer.Deserialize<BoundingBox>(bboxElement.GetRawText(), options);
            }

            Crs crs = null;
            if (rootElement.TryGetProperty("crs", out JsonElement crsElement))
            {
                crs = JsonSerializer.Deserialize<Crs>(crsElement.GetRawText(), options);
            }

            if (crs != null && crs.Equals(Crs.Unspecified))
            {
                crs = null;
            }

            IDictionary<string, object> additionalProperties = null;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.Name != "type" && property.Name != "coordinates" && property.Name != "bbox" && property.Name != "crs" && property.Name != "geometries")
                {
                    additionalProperties ??= new Dictionary<string, object>();
                    additionalProperties[property.Name] = this.ReadValue(property.Value);
                }
            }

            return (typeName, new GeometryParams { BoundingBox = boundingBox, Crs = crs, AdditionalProperties = additionalProperties });
        }

        /// <summary>
        /// Creates the appropriate Geometry subclass based on the type name.
        /// Supports all geometry types including GeometryCollection which contains nested geometries.
        /// </summary>
        private Geometry CreateGeometry(string typeName, GeometryParams geometryParams, JsonElement rootElement, JsonSerializerOptions options)
        {
            switch (typeName)
            {
                case "Point":
                    Position pointCoordinates = JsonSerializer.Deserialize<Position>(rootElement.GetProperty("coordinates").GetRawText(), options);
                    return new Point(pointCoordinates, geometryParams);
                case "MultiPoint":
                    List<Position> multiPointCoordinates = JsonSerializer.Deserialize<List<Position>>(rootElement.GetProperty("coordinates").GetRawText(), options);
                    return new MultiPoint(multiPointCoordinates, geometryParams);
                case "LineString":
                    List<Position> lineStringCoordinates = JsonSerializer.Deserialize<List<Position>>(rootElement.GetProperty("coordinates").GetRawText(), options);
                    return new LineString(lineStringCoordinates, geometryParams);
                case "MultiLineString":
                    List<LineStringCoordinates> multiLineStringCoordinates = JsonSerializer.Deserialize<List<LineStringCoordinates>>(rootElement.GetProperty("coordinates").GetRawText(), options);
                    return new MultiLineString(multiLineStringCoordinates, geometryParams);
                case "Polygon":
                    List<LinearRing> polygonCoordinates = JsonSerializer.Deserialize<List<LinearRing>>(rootElement.GetProperty("coordinates").GetRawText(), options);
                    return new Polygon(polygonCoordinates, geometryParams);
                case "MultiPolygon":
                    List<PolygonCoordinates> multiPolygonCoordinates = JsonSerializer.Deserialize<List<PolygonCoordinates>>(rootElement.GetProperty("coordinates").GetRawText(), options);
                    return new MultiPolygon(multiPolygonCoordinates, geometryParams);
                case "GeometryCollection":
                    List<Geometry> geometries = JsonSerializer.Deserialize<List<Geometry>>(rootElement.GetProperty("geometries").GetRawText(), options);
                    return new GeometryCollection(geometries, geometryParams);
                default:
                    throw new JsonException($"Unknown geometry type: {typeName}");
            }
        }

        /// <summary>
        /// Reads a JSON value and converts it to the appropriate .NET type.
        /// Handles primitive types, objects, and arrays for additional properties.
        /// </summary>
        private object ReadValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l))
                    {
                        return l;
                    }
                    return element.GetDouble();
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                case JsonValueKind.Array:
                default:
                    return element.Clone();
            }
        }
    }
}