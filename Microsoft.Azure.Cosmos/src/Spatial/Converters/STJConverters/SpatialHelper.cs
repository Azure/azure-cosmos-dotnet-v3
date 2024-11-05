// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System.Collections.Generic;
    using System.Text.Json;

    internal static class SpatialHelper
    {
        public static void SerializePartialSpatialObject(Crs crs, int spatialType, BoundingBox boundingBox, IDictionary<string, object> additionalProperties,
                                                                                            Utf8JsonWriter writer, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, crs, options);
            writer.WriteNumber(STJMetaDataFields.Type, spatialType);

            if (boundingBox != null)
            {
                JsonSerializer.Serialize(writer, boundingBox, options);
            }

            if (additionalProperties.Count > 0)
            {
                writer.WritePropertyName(STJMetaDataFields.AdditionalProperties);
                JsonSerializer.Serialize(writer, additionalProperties, options);
            }

        }

        public static (IDictionary<string, object>, Crs, BoundingBox) DeSerializePartialSpatialObject(JsonElement rootElement, JsonSerializerOptions options)
        {
            IDictionary<string, object> additionalProperties = null;
            Crs crs = null;
            BoundingBox boundingBox = null;

            if (rootElement.TryGetProperty(STJMetaDataFields.AdditionalProperties, out JsonElement value))
            {
                additionalProperties = JsonSerializer.Deserialize<IDictionary<string, object>>(value.GetRawText(), options);
            }
            if (rootElement.TryGetProperty(STJMetaDataFields.Crs, out JsonElement crsValue))
            {
                crs = crsValue.ValueKind == JsonValueKind.Null
                        ? Crs.Unspecified
                        : JsonSerializer.Deserialize<Crs>(crsValue.GetRawText(), options);

            }
            if (rootElement.TryGetProperty(STJMetaDataFields.BoundingBox, out JsonElement boxValue))
            {
                boundingBox = JsonSerializer.Deserialize<BoundingBox>(boxValue.GetRawText(), options);
            }

            return (additionalProperties, crs, boundingBox);

        }

    }
    
}
