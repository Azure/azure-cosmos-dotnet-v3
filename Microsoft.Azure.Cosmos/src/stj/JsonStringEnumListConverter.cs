namespace Microsoft.Azure.Cosmos.stj
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class JsonStringEnumListConverter<T> : JsonConverter<IReadOnlyList<T>> where T : struct, Enum
    {
        public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            List<String> list = JsonSerializer.Deserialize<List<string>>(ref reader, options);
            return list?.Select(x => Enum.Parse<T>(x, ignoreCase: true)).ToList();
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
        {
            List<String> stringList = value.Select(e => e.ToString()).ToList();
            JsonSerializer.Serialize(writer, stringList, options);
        }
    }
}
