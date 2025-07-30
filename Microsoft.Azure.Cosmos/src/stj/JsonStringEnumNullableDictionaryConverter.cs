namespace Microsoft.Azure.Cosmos.stj
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class JsonStringEnumNullableDictionaryConverter<TEnum> : JsonConverter<IReadOnlyDictionary<string, TEnum?>> where TEnum : struct, Enum
    {
        public override IReadOnlyDictionary<string, TEnum?>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Dictionary<string, string> dict = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
            return dict?.ToDictionary(
                kvp => kvp.Key,
                kvp => Enum.TryParse<TEnum>(kvp.Value, ignoreCase: true, out var parsed) ? parsed : (TEnum?)null
            );
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, TEnum?> value, JsonSerializerOptions options)
        {
            Dictionary<string, string> dict = value.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString()
            );
            JsonSerializer.Serialize(writer, dict, options);
        }
    }
}
