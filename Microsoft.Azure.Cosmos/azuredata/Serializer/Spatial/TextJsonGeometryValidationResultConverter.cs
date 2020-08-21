//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Linq;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal sealed class TextJsonGeometryValidationResultConverter : JsonConverter<IsValidDetailedResult>
    {
        public override IsValidDetailedResult Read(
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

            if (!root.TryGetProperty(JsonEncodedStrings.Valid.EncodedUtf8Bytes, out JsonElement validElement))
            {
                throw new InvalidOperationException("is valid needs to be provided.");
            }

            bool isValid = validElement.GetBoolean();

            string reason;
            if (root.TryGetProperty(JsonEncodedStrings.Reason.EncodedUtf8Bytes, out JsonElement reasonElement))
            {
                reason = reasonElement.GetString();
            }
            else
            {
                reason = null;
            }

            return new IsValidDetailedResult(isValid, reason);
        }

        public override void Write(
            Utf8JsonWriter writer,
            IsValidDetailedResult geometryValidationResult,
            JsonSerializerOptions options)
        {
            if (geometryValidationResult == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteBoolean(JsonEncodedStrings.Valid, geometryValidationResult.IsValid);

            writer.WriteString(JsonEncodedStrings.Reason, geometryValidationResult.Reason);

            writer.WriteEndObject();
        }
    }
}
