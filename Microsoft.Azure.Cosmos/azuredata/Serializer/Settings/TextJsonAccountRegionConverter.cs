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

    internal class TextJsonAccountRegionConverter : JsonConverter<AccountRegion>
    {
        public override AccountRegion Read(
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
            return TextJsonAccountRegionConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            AccountRegion region,
            JsonSerializerOptions options)
        {
            TextJsonAccountRegionConverter.WritePropertyValues(writer, region);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            AccountRegion region)
        {
            if (region == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteString(Constants.Properties.Name, region.Name);

            writer.WriteString(Constants.Properties.DatabaseAccountEndpoint, region.Endpoint);

            writer.WriteEndObject();
        }

        public static AccountRegion ReadProperty(JsonElement root)
        {
            AccountRegion region = new AccountRegion();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonAccountRegionConverter.ReadPropertyValue(region, property);
            }

            return region;
        }

        private static void ReadPropertyValue(
            AccountRegion region,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Name))
            {
                region.Name = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.DatabaseAccountEndpoint))
            {
                region.Endpoint = property.Value.GetString();
            }
        }
    }
}
