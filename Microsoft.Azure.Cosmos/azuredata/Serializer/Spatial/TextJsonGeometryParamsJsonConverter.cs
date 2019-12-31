//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal sealed class TextJsonGeometryParamsJsonConverter : JsonConverter<GeometryParams>
    {
        public override GeometryParams Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonGeometryParamsJsonConverter.ReadProperty(root, options);
        }

        public override void Write(
            Utf8JsonWriter writer,
            GeometryParams geometryParams,
            JsonSerializerOptions options)
        {
            TextJsonGeometryParamsJsonConverter.WritePropertyValues(writer, geometryParams, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            GeometryParams geometryParams,
            JsonSerializerOptions options)
        {
            if (geometryParams == null)
            {
                return;
            }

            writer.WriteStartObject();

            if (geometryParams.Crs != null)
            {
                writer.WritePropertyName("crs");
                TextJsonCrsConverter.WritePropertyValues(writer, geometryParams.Crs, options);
            }

            if (geometryParams.BoundingBox != null)
            {
                writer.WritePropertyName("bbox");
                TextJsonBoundingBoxConverter.WritePropertyValues(writer, geometryParams.BoundingBox, options);
            }

            if (geometryParams.AdditionalProperties != null)
            {
                writer.WritePropertyName("properties");
                JsonSerializer.Serialize(writer, geometryParams.AdditionalProperties, options);
            }

            writer.WriteEndObject();
        }

        public static GeometryParams ReadProperty(
            JsonElement root,
            JsonSerializerOptions options)
        {
            GeometryParams geometryParams = new GeometryParams();
            if (root.TryGetProperty("crs", out JsonElement crsElement))
            {
                geometryParams.Crs = TextJsonCrsConverter.ReadProperty(crsElement);
            }

            if (root.TryGetProperty("bbox", out JsonElement bboxElement))
            {
                geometryParams.BoundingBox = TextJsonBoundingBoxConverter.ReadProperty(bboxElement);
            }

            if (root.TryGetProperty("properties", out JsonElement propertiesElement))
            {
                geometryParams.AdditionalProperties = TextJsonObjectToPrimitiveConverter.DeserializeDictionary(propertiesElement.GetRawText());
            }

            return geometryParams;
        }
    }
}
