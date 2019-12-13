//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonExcludedPathConverter : JsonConverter<ExcludedPath>
    {
        public override ExcludedPath Read(
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
            return TextJsonExcludedPathConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ExcludedPath path,
            JsonSerializerOptions options)
        {
            TextJsonExcludedPathConverter.WritePropertyValues(writer, path, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            ExcludedPath compositePath,
            JsonSerializerOptions options)
        {
            if (compositePath == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Path, compositePath.Path);

            writer.WriteEndObject();
        }

        public static ExcludedPath ReadProperty(JsonElement root)
        {
            ExcludedPath path = new ExcludedPath();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonExcludedPathConverter.ReadPropertyValue(path, property);
            }

            return path;
        }

        private static void ReadPropertyValue(
            ExcludedPath path,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Path))
            {
                path.Path = property.Value.GetString();
            }
        }
    }
}
