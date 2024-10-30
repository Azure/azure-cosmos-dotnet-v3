// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    using static System.Text.Json.JsonElement;

    internal class BoundingBoxSTJJsonConverter : JsonConverter<BoundingBox>
    {
        public override BoundingBox Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            Position min = null;
            Position max = null;

            JsonElement rootElement = JsonDocument.ParseValue(ref reader).RootElement;
            foreach (JsonProperty property in rootElement.EnumerateObject())
            {
                if (property.NameEquals("min"))
                {
                    min = System.Text.Json.JsonSerializer.Deserialize<Position>(property.Value.ToString(), options);
                    
                }
                else if (property.NameEquals("max"))
                {
                    max = System.Text.Json.JsonSerializer.Deserialize<Position>(property.Value.ToString(), options);

                }
            }

            return new BoundingBox(min, max);
        }

        public override void Write(Utf8JsonWriter writer, BoundingBox box, JsonSerializerOptions options)
        {
            if (box == null)
            {
                return;
            }

            writer.WriteStartObject("boundingBox");
            
            writer.WriteStartObject("min");
            System.Text.Json.JsonSerializer.Serialize(writer, box.Min, options);
            writer.WriteEndObject();

            writer.WriteStartObject("max");
            System.Text.Json.JsonSerializer.Serialize(writer, box.Max, options);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
