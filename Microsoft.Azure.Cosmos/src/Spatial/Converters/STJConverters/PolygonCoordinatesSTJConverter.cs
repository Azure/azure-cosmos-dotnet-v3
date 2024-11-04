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
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type PolygonCoordinates/>.
    /// </summary>
    internal class PolygonCoordinatesSTJConverter : JsonConverter<PolygonCoordinates>
    {
        public override PolygonCoordinates Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            IList<LinearRing> linearRings = null;
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals(STJMetaDataFields.Rings))
                {
                    linearRings = new List<LinearRing>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        LinearRing ring = JsonSerializer.Deserialize<LinearRing>(arrayElement.GetRawText(), options);
                        linearRings.Add(ring);
                    }
                }

            }
            return new PolygonCoordinates(linearRings);

        }
        public override void Write(Utf8JsonWriter writer, PolygonCoordinates polygonCoordinates, JsonSerializerOptions options)
        {
            writer.WriteStartArray(STJMetaDataFields.Rings);
            foreach (LinearRing ring in polygonCoordinates.Rings)
            {
                writer.WriteStartObject();
                JsonSerializer.Serialize(writer, ring, options);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

        }

    }

}
