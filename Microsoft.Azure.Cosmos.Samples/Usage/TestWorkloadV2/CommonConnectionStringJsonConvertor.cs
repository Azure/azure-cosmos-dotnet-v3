namespace TestWorkloadV2
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class CommonConnectionStringJsonConvertor : JsonConverter<CommonConnectionString>
    {
        public override CommonConnectionString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // unused
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, CommonConnectionString value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetForLogging());
        }
    }
}
