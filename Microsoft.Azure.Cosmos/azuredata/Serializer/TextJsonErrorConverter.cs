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

    internal sealed class TextJsonErrorConverter : JsonConverter<Error>
    {
        public static Lazy<JsonSerializerOptions> ErrorSerializationOptions = new Lazy<JsonSerializerOptions>(() =>
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.Converters.Add(new TextJsonErrorConverter());
            return jsonSerializerOptions;
        });

        public override Error Read(
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
            Error error = new Error();

            if (document.RootElement.TryGetProperty(Constants.Properties.Code, out JsonElement codeElement))
            {
                error.Code = codeElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.Message, out JsonElement messageElement))
            {
                error.Message = messageElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.ErrorDetails, out JsonElement errorDetailsElement))
            {
                error.ErrorDetails = errorDetailsElement.GetString();
            }

            if (document.RootElement.TryGetProperty(Constants.Properties.AdditionalErrorInfo, out JsonElement additionalErrorInfoElement))
            {
                error.AdditionalErrorInfo = additionalErrorInfoElement.GetString();
            }

            return error;
        }

        public override void Write(
            Utf8JsonWriter writer,
            Error value,
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                return;
            }

            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(value.Code))
            {
                writer.WriteString(Constants.Properties.Code, value.Code);
            }

            if (!string.IsNullOrEmpty(value.Message))
            {
                writer.WriteString(Constants.Properties.Message, value.Message);
            }

            if (!string.IsNullOrEmpty(value.ErrorDetails))
            {
                writer.WriteString(Constants.Properties.ErrorDetails, value.ErrorDetails);
            }

            if (!string.IsNullOrEmpty(value.AdditionalErrorInfo))
            {
                writer.WriteString(Constants.Properties.AdditionalErrorInfo, value.AdditionalErrorInfo);
            }

            writer.WriteEndObject();
        }
    }
}
