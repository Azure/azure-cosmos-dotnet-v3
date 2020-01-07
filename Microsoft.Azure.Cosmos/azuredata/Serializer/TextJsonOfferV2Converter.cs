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

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.OfferVersion.EncodedUtf8Bytes, out JsonElement versionElement))
            {
                offer.OfferVersion = versionElement.GetString();
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.ResourceLink.EncodedUtf8Bytes, out JsonElement resourceLinkElement))
            {
                offer.ResourceLink = resourceLinkElement.GetString();
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.OfferType.EncodedUtf8Bytes, out JsonElement typeElement))
            {
                offer.OfferType = typeElement.GetString();
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.OfferResourceId.EncodedUtf8Bytes, out JsonElement offerResourceIdElement))
            {
                offer.OfferResourceId = offerResourceIdElement.GetString();
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.OfferContent.EncodedUtf8Bytes, out JsonElement offerContentElement))
            {
                offer.Content = TextJsonOfferV2Converter.ReadOfferContent(offerContentElement);
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.SelfLink.EncodedUtf8Bytes, out JsonElement selfLinkElement))
            {
                offer.SelfLink = selfLinkElement.GetString();
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.Id.EncodedUtf8Bytes, out JsonElement idElement))
            {
                offer.Id = idElement.GetString();
            }

            if (document.RootElement.TryGetProperty(JsonEncodedStrings.RId.EncodedUtf8Bytes, out JsonElement resourceIdElement))
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
                writer.WriteString(JsonEncodedStrings.OfferVersion, value.OfferVersion);
            }

            if (!string.IsNullOrEmpty(value.ResourceLink))
            {
                writer.WriteString(JsonEncodedStrings.ResourceLink, value.ResourceLink);
            }

            if (!string.IsNullOrEmpty(value.OfferType))
            {
                writer.WriteString(JsonEncodedStrings.OfferType, value.OfferType);
            }

            if (!string.IsNullOrEmpty(value.OfferResourceId))
            {
                writer.WriteString(JsonEncodedStrings.OfferResourceId, value.OfferResourceId);
            }

            if (value.Content != null)
            {
                TextJsonOfferV2Converter.WriteOfferContent(writer, value.Content, JsonEncodedStrings.OfferContent);
            }

            if (!string.IsNullOrEmpty(value.SelfLink))
            {
                writer.WriteString(JsonEncodedStrings.SelfLink, value.SelfLink);
            }

            TextJsonSettingsHelper.WriteId(writer, value.Id);
            TextJsonSettingsHelper.WriteResourceId(writer, value.ResourceId);

            writer.WriteEndObject();
        }

        public static OfferContentV2 ReadOfferContent(JsonElement jsonElement)
        {
            int? offerThroughput = null;
            bool? offerIsRUPerMinuteThroughputEnabled = null;
            if (jsonElement.TryGetProperty(JsonEncodedStrings.OfferThroughput.EncodedUtf8Bytes, out JsonElement offerThroughputElement))
            {
                offerThroughput = offerThroughputElement.GetInt32();
            }

            if (jsonElement.TryGetProperty(JsonEncodedStrings.OfferIsRUPerMinuteThroughputEnabled.EncodedUtf8Bytes, out JsonElement offerIsRUPerMinuteThroughputEnabledElement)
                && offerIsRUPerMinuteThroughputEnabledElement.ValueKind != JsonValueKind.Null)
            {
                offerIsRUPerMinuteThroughputEnabled = offerIsRUPerMinuteThroughputEnabledElement.GetBoolean();
            }

            return offerThroughput.HasValue ? new OfferContentV2(offerThroughput.Value, offerIsRUPerMinuteThroughputEnabled) : new OfferContentV2();
        }

        public static void WriteOfferContent(
            Utf8JsonWriter writer,
            OfferContentV2 content,
            JsonEncodedText propertyName)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            if (content.OfferThroughput > 0)
            {
                writer.WriteNumber(JsonEncodedStrings.OfferThroughput, content.OfferThroughput);
            }

            if (content.OfferIsRUPerMinuteThroughputEnabled.HasValue)
            {
                writer.WriteBoolean(JsonEncodedStrings.OfferIsRUPerMinuteThroughputEnabled, content.OfferIsRUPerMinuteThroughputEnabled.Value);
            }

            writer.WriteEndObject();
        }
    }
}
