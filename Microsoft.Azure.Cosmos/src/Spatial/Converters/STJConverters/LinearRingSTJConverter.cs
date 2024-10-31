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

    internal class LinearRingSTJConverter : JsonConverter<LinearRing>
    {
        public override LinearRing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            IList<Position> positions = null;
            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("positions"))
                {
                    positions = new List<Position>();
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        Position pos = System.Text.Json.JsonSerializer.Deserialize<Position>(arrayElement.GetRawText(), options);
                        positions.Add(pos);
                    }
                }

            }
            return new LinearRing(positions);

        }
        public override void Write(Utf8JsonWriter writer, LinearRing linearRing, JsonSerializerOptions options)
        {
            if (linearRing == null)
            {
                return;
            }
            writer.WriteStartArray("positions");
            foreach (Position position in linearRing.Positions)
            {
                writer.WriteStartObject();
                System.Text.Json.JsonSerializer.Serialize(writer, position, options);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

        }

    }

}
