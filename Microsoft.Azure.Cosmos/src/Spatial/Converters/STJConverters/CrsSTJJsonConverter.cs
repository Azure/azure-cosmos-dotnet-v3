// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Documents;
    using static System.Text.Json.JsonElement;

    internal class CrsSTJJsonConverter : JsonConverter<Crs>
    {
        public override Crs Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Crs.Unspecified;
            }

            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            JsonElement properties = rootElement.GetProperty("properties");
            if (properties.ValueKind == JsonValueKind.Null || properties.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            JsonElement crsType = rootElement.GetProperty("type");
            if (crsType.ValueKind == JsonValueKind.Null || crsType.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

            switch (crsType.GetString())
            {
                case "name":
                    string crsName = properties.GetProperty("name").GetString();
                    return new NamedCrs(crsName);

                case "link":
                    string crsHref = properties.GetProperty("href").GetString();
                    string crsHrefType = properties.GetProperty("type").GetString();
                    return new LinkedCrs(crsHref, crsHrefType);

                default:
                    throw new JsonException(RMResources.SpatialFailedToDeserializeCrs);
            }

        }
        public override void Write(Utf8JsonWriter writer, Crs crs, JsonSerializerOptions options)
        {
            if (crs == null)
            {
                return;
            }

            switch (crs.Type)
            {
                case CrsType.Linked:
                    writer.WriteStartObject("crs");
                    LinkedCrs linkedCrs = (LinkedCrs)crs;
                    writer.WriteString("type", "link");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WriteString("href", linkedCrs.Href);
                    if (linkedCrs.HrefType != null)
                    {
                        writer.WriteString("type", linkedCrs.HrefType);
                    }

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;

                case CrsType.Named:
                    writer.WriteStartObject("crs");
                    NamedCrs namedCrs = (NamedCrs)crs;
                    writer.WriteString("type", "name");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WriteString("name", namedCrs.Name);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                    break;

                case CrsType.Unspecified:
                    writer.WriteNull("crs");
                    break;
            }

        }
    }
}
