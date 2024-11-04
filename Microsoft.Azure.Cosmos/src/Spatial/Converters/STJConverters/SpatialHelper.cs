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

    }
    
}
