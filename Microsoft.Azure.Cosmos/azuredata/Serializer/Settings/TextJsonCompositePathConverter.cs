//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonCompositePathConverter : JsonConverter<CompositePath>
    {
        public override CompositePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonCompositePathConverter.ReadProperty(root);
        }

        public override void Write(Utf8JsonWriter writer, CompositePath compositePath, JsonSerializerOptions options)
        {
            TextJsonCompositePathConverter.WritePropertyValues(writer, compositePath, options);
        }

        public static void WritePropertyValues(Utf8JsonWriter writer, CompositePath compositePath, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Path, compositePath.Path);

            writer.WritePropertyName(Constants.Properties.Order);
            writer.WriteStringValue(JsonSerializer.Serialize(compositePath.Order, options));

            writer.WriteEndObject();
        }

        public static CompositePath ReadProperty(JsonElement root)
        {
            CompositePath compositePath = new CompositePath();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonCompositePathConverter.ReadPropertyValue(compositePath, property);
            }

            return compositePath;
        }

        private static void ReadPropertyValue(CompositePath compositePath, JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Path))
            {
                compositePath.Path = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.Order))
            {
                if (Enum.TryParse<CompositePathSortOrder>(property.Value.GetString(), out CompositePathSortOrder compositePathSortOrder))
                {
                    compositePath.Order = compositePathSortOrder;
                }
            }
        }
    }
}
