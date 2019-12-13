//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonIncludedPathConverter : JsonConverter<IncludedPath>
    {
        public override IncludedPath Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonIncludedPathConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            IncludedPath path,
            JsonSerializerOptions options)
        {
            TextJsonIncludedPathConverter.WritePropertyValues(writer, path, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            IncludedPath path,
            JsonSerializerOptions options)
        {
            if (path == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Path, path.Path);

            if (path.Indexes != null)
            {
                writer.WritePropertyName(Constants.Properties.Indexes);
                writer.WriteStartArray();
                foreach (Index index in path.Indexes)
                {
                    TextJsonIndexConverter.WritePropertyValues(writer, index, options);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        public static IncludedPath ReadProperty(JsonElement root)
        {
            IncludedPath path = new IncludedPath();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonIncludedPathConverter.ReadPropertyValue(path, property);
            }

            return path;
        }

        private static void ReadPropertyValue(
            IncludedPath path,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Path))
            {
                path.Path = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.Indexes))
            {
                path.Indexes = new Collection<Index>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    path.Indexes.Add(TextJsonIndexConverter.ReadProperty(item));
                }
            }
        }
    }
}
