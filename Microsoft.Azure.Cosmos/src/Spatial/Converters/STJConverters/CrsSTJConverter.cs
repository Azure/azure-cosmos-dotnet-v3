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
    /// Converter used to support System.Text.Json de/serialization of type Crs/>.
    /// </summary>
    internal class CrsSTJConverter : JsonConverter<Crs>
    {
        public override Crs Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }

            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            JsonElement properties = rootElement.GetProperty(STJMetaDataFields.Properties);
            if (properties.ValueKind == JsonValueKind.Null || properties.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            JsonElement crsType = rootElement.GetProperty(STJMetaDataFields.Type);
            if (crsType.ValueKind == JsonValueKind.Null || crsType.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            switch (crsType.GetString())
            {
                case STJMetaDataFields.Name:
                    string crsName = properties.GetProperty(STJMetaDataFields.Name).GetString();
                    return new NamedCrs(crsName);

                case STJMetaDataFields.Link:
                    string crsHref = properties.GetProperty(STJMetaDataFields.Href).GetString();
                    if (properties.TryGetProperty(STJMetaDataFields.Type, out JsonElement crsHrefType))
                    {
                        return new LinkedCrs(crsHref, crsHrefType.GetString());
                    }
                    return new LinkedCrs(crsHref);

                default:
                    throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

        }
        public override void Write(Utf8JsonWriter writer, Crs crs, JsonSerializerOptions options)
        {
            switch (crs.Type)
            {
                case CrsType.Linked:
                    writer.WriteStartObject(STJMetaDataFields.Crs);
                    LinkedCrs linkedCrs = (LinkedCrs)crs;
                    writer.WriteString(STJMetaDataFields.Type, STJMetaDataFields.Link);
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

                case CrsType.Named:
                    writer.WriteStartObject(STJMetaDataFields.Crs);
                    NamedCrs namedCrs = (NamedCrs)crs;
                    writer.WriteString(STJMetaDataFields.Type, STJMetaDataFields.Name);
                    writer.WritePropertyName(STJMetaDataFields.Properties);
                    writer.WriteStartObject();
                    writer.WriteString(STJMetaDataFields.Name, namedCrs.Name);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                    break;

                case CrsType.Unspecified:
                    writer.WriteNull(STJMetaDataFields.Crs);
                    break;
            }

        }
    }
}
