// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal class PolygonCoordinatesSTJConverter : JsonConverter<PolygonCoordinates>
    {
        public override PolygonCoordinates Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            IList<LinearRing> linearRings = null;
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("rings"))
                {
                    linearRings = new List<LinearRing>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        LinearRing ring = System.Text.Json.JsonSerializer.Deserialize<LinearRing>(arrayElement.GetRawText(), options);
                        linearRings.Add(ring);
                    }
                }

            }
            return new PolygonCoordinates(linearRings);

        }
        public override void Write(Utf8JsonWriter writer, PolygonCoordinates polygonCoordinates, JsonSerializerOptions options)
        {
            if (polygonCoordinates == null)
            {
                return;
            }
            writer.WriteStartArray("rings");
            foreach (LinearRing ring in polygonCoordinates.Rings)
            {
                writer.WriteStartObject();
                System.Text.Json.JsonSerializer.Serialize(writer, ring, options);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

        }

    }

}
