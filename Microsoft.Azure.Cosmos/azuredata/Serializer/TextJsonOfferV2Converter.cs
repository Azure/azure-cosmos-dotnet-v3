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

    internal sealed class TextJsonOfferV2Converter : JsonConverter<OfferV2>
    {
        public override OfferV2 Read(
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
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonInvalidToken));
            }

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            OfferV2 offer = new OfferV2();

            if (document.RootElement.TryGetProperty(Constants.Properties.OfferVersion, out JsonElement versionElement))
            {
                offer.OfferVersion = versionElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.ResourceLink, out JsonElement resourceLinkElement))
            {
                offer.ResourceLink = resourceLinkElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.OfferType, out JsonElement typeElement))
            {
                offer.OfferType = typeElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.OfferResourceId, out JsonElement offerResourceIdElement))
            {
                offer.OfferResourceId = offerResourceIdElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.OfferContent, out JsonElement offerContentElement))
            {
                offer.Content = TextJsonOfferV2Converter.ReadOfferContent(offerContentElement);
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.SelfLink, out JsonElement selfLinkElement))
            {
                offer.SelfLink = selfLinkElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.Id, out JsonElement idElement))
            {
                offer.Id = idElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.RId, out JsonElement resourceIdElement))
            {
                offer.ResourceId = resourceIdElement.GetString();
            }

            return offer;
        }

        public override void Write(
            Utf8JsonWriter writer,
            OfferV2 value,
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                return;
            }

            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(value.OfferVersion))
            {
                writer.WriteString(Constants.Properties.OfferVersion, value.OfferVersion);
            }

            if (!string.IsNullOrEmpty(value.ResourceLink))
            {
                writer.WriteString(Constants.Properties.ResourceLink, value.ResourceLink);
            }

            if (!string.IsNullOrEmpty(value.OfferType))
            {
                writer.WriteString(Constants.Properties.OfferType, value.OfferType);
            }

            if (!string.IsNullOrEmpty(value.OfferResourceId))
            {
                writer.WriteString(Constants.Properties.OfferResourceId, value.OfferResourceId);
            }

            if (value.Content != null)
            {
                TextJsonOfferV2Converter.WriteOfferContent(writer, value.Content, Constants.Properties.OfferContent);
            }

            if (!string.IsNullOrEmpty(value.Id))
            {
                writer.WriteString(Constants.Properties.Id, value.Id);
            }

            if (!string.IsNullOrEmpty(value.SelfLink))
            {
                writer.WriteString(Constants.Properties.SelfLink, value.SelfLink);
            }

            if (!string.IsNullOrEmpty(value.ResourceId))
            {
                writer.WriteString(Constants.Properties.RId, value.ResourceId);
            }

            writer.WriteEndObject();
        }

        public static OfferContentV2 ReadOfferContent(JsonElement jsonElement)
        {
            int? offerThroughput = null;
            bool? offerIsRUPerMinuteThroughputEnabled = null;
            if (jsonElement.TryGetProperty(Constants.Properties.OfferThroughput, out JsonElement offerThroughputElement))
            {
                offerThroughput = offerThroughputElement.GetInt32();
            }

            if (jsonElement.TryGetProperty(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, out JsonElement offerIsRUPerMinuteThroughputEnabledElement)
                && offerIsRUPerMinuteThroughputEnabledElement.ValueKind != JsonValueKind.Null)
            {
                offerIsRUPerMinuteThroughputEnabled = offerIsRUPerMinuteThroughputEnabledElement.GetBoolean();
            }

            return offerThroughput.HasValue ? new OfferContentV2(offerThroughput.Value, offerIsRUPerMinuteThroughputEnabled) : new OfferContentV2();
        }

        public static void WriteOfferContent(
            Utf8JsonWriter writer,
            OfferContentV2 content,
            string propertyName)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            if (content.OfferThroughput > 0)
            {
                writer.WriteNumber(Constants.Properties.OfferThroughput, content.OfferThroughput);
            }

            if (content.OfferIsRUPerMinuteThroughputEnabled.HasValue)
            {
                writer.WriteBoolean(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, content.OfferIsRUPerMinuteThroughputEnabled.Value);
            }

            writer.WriteEndObject();
        }
    }
}
