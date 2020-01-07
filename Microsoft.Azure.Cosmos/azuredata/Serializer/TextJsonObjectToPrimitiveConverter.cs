//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// System.Text.Json does not honor object as Json.Net https://github.com/dotnet/corefx/issues/39953
    /// </summary>
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
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return TextJsonObjectToPrimitiveConverter.ReadProperty(document.RootElement);
        }

        public override void Write(
            Utf8JsonWriter writer,
            object value,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public static object ReadProperty(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (jsonElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                if (jsonElement.TryGetInt64(out long longValue))
                {
                    return longValue;
                }
                else if (jsonElement.TryGetInt32(out int intValue))
                {
                    return intValue;
                }

                return jsonElement.GetDouble();
            }

            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                if (jsonElement.TryGetDateTime(out DateTime datetime))
                {
                    // If an offset was provided, use DateTimeOffset.
                    if (datetime.Kind == DateTimeKind.Local)
                    {
                        if (jsonElement.TryGetDateTimeOffset(out DateTimeOffset datetimeOffset))
                        {
                            return datetimeOffset;
                        }
                    }

                    return datetime;
                }

                return jsonElement.GetString();
            }

            return jsonElement.Clone();
        }

        public static Dictionary<string, object> DeserializeDictionary(string serializedDictionary) => JsonSerializer.Deserialize<Dictionary<string, object>>(serializedDictionary, TextJsonObjectToPrimitiveConverter.DictionarySerializeOptions.Value);

        public static void SerializeDictionary(
            Utf8JsonWriter writer,
            IDictionary<string, object> dictionary,
            JsonSerializerOptions options)
        {
            foreach (KeyValuePair<string, object> kvp in dictionary)
            {
                writer.WritePropertyName(kvp.Key);
                JsonSerializer.Serialize(writer, kvp.Value, options);
            }
        }
    }
}
