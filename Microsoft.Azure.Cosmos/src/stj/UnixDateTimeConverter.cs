namespace Microsoft.Azure.Cosmos.stj
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class UnixDateTimeConverter : JsonConverter<DateTime?>
    {
        private static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out long seconds))
            {
                throw new JsonException("Expected Unix timestamp in seconds.");
            }

            return UnixEpoch.AddSeconds(seconds);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            long seconds = (long)(value.Value.ToUniversalTime() - UnixEpoch).TotalSeconds;
            writer.WriteNumberValue(seconds);
        }
    }
}
