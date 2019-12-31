//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class TextJsonObjectToPrimitiveConverter : JsonConverter<object>
    {
        private static Lazy<JsonSerializerOptions> DictionarySerializeOptions = new Lazy<JsonSerializerOptions>(() =>
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.Converters.Add(new TextJsonObjectToPrimitiveConverter());
            return jsonSerializerOptions;
        });

        public override object Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True)
            {
                return true;
            }

            if (reader.TokenType == JsonTokenType.False)
            {
                return false;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                else if (reader.TryGetInt64(out long longValue))
                {
                    return longValue;
                }

                return reader.GetDouble();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                if (reader.TryGetDateTime(out DateTime datetime))
                {
                    // If an offset was provided, use DateTimeOffset.
                    if (datetime.Kind == DateTimeKind.Local)
                    {
                        if (reader.TryGetDateTimeOffset(out DateTimeOffset datetimeOffset))
                        {
                            return datetimeOffset;
                        }
                    }

                    return datetime;
                }

                return reader.GetString();
            }

            // Use JsonElement as fallback.
            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                return document.RootElement.Clone();
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            object value,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// System.Text.Json does not honor object as Json.Net https://github.com/dotnet/corefx/issues/39953
        /// </summary>
        public static Dictionary<string, object> DeserializeDictionary(string serializedDictionary) => JsonSerializer.Deserialize<Dictionary<string, object>>(serializedDictionary, TextJsonObjectToPrimitiveConverter.DictionarySerializeOptions.Value);
    }
}
