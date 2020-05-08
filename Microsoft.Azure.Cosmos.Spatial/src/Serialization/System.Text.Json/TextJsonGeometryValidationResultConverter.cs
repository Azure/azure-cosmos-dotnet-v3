//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal sealed class TextJsonGeometryValidationResultConverter : JsonConverter<GeometryValidationResult>
    {
        public override GeometryValidationResult Read(
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

            GeometryValidationResult geometryValidationResult = new GeometryValidationResult();
            if (root.TryGetProperty(JsonEncodedStrings.Valid.EncodedUtf8Bytes, out JsonElement validElement))
            {
                geometryValidationResult.IsValid = validElement.GetBoolean();
            }

            if (root.TryGetProperty(JsonEncodedStrings.Reason.EncodedUtf8Bytes, out JsonElement reasonElement))
            {
                geometryValidationResult.Reason = reasonElement.GetString();
            }

            return geometryValidationResult;
        }

        public override void Write(
            Utf8JsonWriter writer,
            GeometryValidationResult geometryValidationResult,
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
