// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json serialization/deserialization of Crs (Coordinate Reference System).
    /// Handles NamedCrs, LinkedCrs, and Unspecified CRS types.
    /// Ensures output format matches Newtonsoft.Json exactly.
    /// </summary>
    internal sealed class CrsSTJConverter : JsonConverter<Crs>
    {
        public override bool HandleNull => true;
        
        /// <summary>
        /// Deserializes a CRS from JSON.
        /// Supports named CRS (e.g., EPSG:4326) and linked CRS with optional type.
        /// Returns Crs.Unspecified for null values.
        /// </summary>
        public override Crs Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Crs.Unspecified;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }

            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            if (!rootElement.TryGetProperty(STJMetaDataFields.Properties, out JsonElement properties) || (properties.ValueKind != JsonValueKind.Object))
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            if (!rootElement.TryGetProperty(STJMetaDataFields.Type, out JsonElement crsType) || crsType.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            switch (crsType.GetString())
            {
                case "name":
                    if (!properties.TryGetProperty(STJMetaDataFields.Name, out JsonElement crsName) || crsName.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }
                    return new NamedCrs(crsName.GetString());

                case "link":
                    if (!properties.TryGetProperty(STJMetaDataFields.Href, out JsonElement crsHref) || crsHref.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    if (properties.TryGetProperty(STJMetaDataFields.Type, out JsonElement crsHrefType))
                    {
                        if (crsHrefType.ValueKind != JsonValueKind.String)
                        {
                            throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
                        }
                        return new LinkedCrs(crsHref.GetString(), crsHrefType.GetString());
                    }
                    return new LinkedCrs(crsHref.GetString());

                default:
                    throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

        }
        /// <summary>
        /// Serializes a CRS to JSON.
        /// Outputs different JSON structures based on CRS type (Named, Linked, or Unspecified).
        /// </summary>
        public override void Write(Utf8JsonWriter writer, Crs crs, JsonSerializerOptions options)
        {
            if (crs == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (crs.Type)
            {
                case CrsType.Linked:
                    {
                        writer.WriteStartObject();
                        LinkedCrs linkedCrs = (LinkedCrs)crs;
                        writer.WriteString(STJMetaDataFields.Type, "link");
                        writer.WritePropertyName(STJMetaDataFields.Properties);
                        writer.WriteStartObject();
                        writer.WriteString(STJMetaDataFields.Href, linkedCrs.Href);
                        if (linkedCrs.HrefType != null)
                        {
                            writer.WriteString(STJMetaDataFields.Type, linkedCrs.HrefType);
                        }

                        writer.WriteEndObject();
                        writer.WriteEndObject();
                        break;
                    }
                case CrsType.Named:
                    {
                        writer.WriteStartObject();
                        NamedCrs namedCrs = (NamedCrs)crs;
                        writer.WriteString(STJMetaDataFields.Type, "name");
                        writer.WritePropertyName(STJMetaDataFields.Properties);
                        writer.WriteStartObject();
                        writer.WriteString(STJMetaDataFields.Name, namedCrs.Name);
                        writer.WriteEndObject();
                        writer.WriteEndObject();
                        break;
                    }
                case CrsType.Unspecified:
                    writer.WriteNullValue();
                    break;
            }

        }
    }
}
