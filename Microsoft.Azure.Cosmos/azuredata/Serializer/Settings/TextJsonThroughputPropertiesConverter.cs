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

    internal class TextJsonThroughputPropertiesConverter : JsonConverter<ThroughputProperties>
    {
        public override ThroughputProperties Read(
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
            ThroughputProperties setting = new ThroughputProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonThroughputPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            ThroughputProperties setting,
            JsonSerializerOptions options)
        {
            if (setting == null)
            {
                return;
            }

            writer.WriteStartObject();

            TextJsonSettingsHelper.WriteETag(writer, setting.ETag);

            TextJsonSettingsHelper.WriteResourceId(writer, setting.OfferRID);

            TextJsonSettingsHelper.WriteLastModified(writer, setting.LastModified, options);

            if (!string.IsNullOrEmpty(setting.ResourceRID))
            {
                writer.WriteString(Constants.Properties.OfferResourceId, setting.ResourceRID);
            }
            if (setting.Content != null)
            {
                writer.WritePropertyName(Constants.Properties.Content);
                writer.WriteStartObject();
                if (setting.Content.OfferThroughput > 0)
                {
                    writer.WriteNumber(Constants.Properties.OfferThroughput, setting.Content.OfferThroughput);
                }

                if (setting.Content.OfferIsRUPerMinuteThroughputEnabled.HasValue)
                {
                    writer.WriteBoolean(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, setting.Content.OfferIsRUPerMinuteThroughputEnabled.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            ThroughputProperties setting,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.ETag))
            {
                setting.ETag = TextJsonSettingsHelper.ReadETag(property);
            }
            else if (property.NameEquals(Constants.Properties.LastModified))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property);
            }
            else if (property.NameEquals(Constants.Properties.RId))
            {
                setting.OfferRID = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.OfferResourceId))
            {
                setting.ResourceRID = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.Content))
            {
                int? offerThroughput = null;
                bool? offerIsRUPerMinuteThroughputEnabled = null;
                if (property.Value.TryGetProperty(Constants.Properties.OfferThroughput, out JsonElement offerThroughputElement))
                {
                    offerThroughput = offerThroughputElement.GetInt32();
                }

                if (property.Value.TryGetProperty(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, out JsonElement offerIsRUPerMinuteThroughputEnabledElement)
                    && offerIsRUPerMinuteThroughputEnabledElement.ValueKind != JsonValueKind.Null)
                {
                    offerIsRUPerMinuteThroughputEnabled = offerIsRUPerMinuteThroughputEnabledElement.GetBoolean();
                }

                setting.Content = offerThroughput.HasValue ? new OfferContentV2(offerThroughput.Value, offerIsRUPerMinuteThroughputEnabled) : new OfferContentV2();
            }
        }
    }
}
