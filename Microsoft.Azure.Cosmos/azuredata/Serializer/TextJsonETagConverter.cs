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

    internal sealed class TextJsonETagConverter : JsonConverter<ETag?>
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(ETag?).IsAssignableFrom(objectType);
        }

        public override ETag? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonInvalidStringCharacter));
            }

            return new ETag(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, ETag? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                JsonSerializer.Serialize(writer, value.ToString(), options);
            }
        }
    }
}