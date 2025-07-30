namespace Microsoft.Azure.Cosmos.stj
{
    using System;
    using System.IO;
    using System.Text.Json;

    public sealed class CosmosSystemTextJsonSerializer : CosmosSerializer
    {
        private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = CosmosJsonContext.Default
        };

        private readonly JsonSerializerOptions serializerOptions;

        public CosmosSystemTextJsonSerializer(JsonSerializerOptions? options = null)
        {
            this.serializerOptions = options ?? DefaultOptions;
        }

        public override T FromStream<T>(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            return JsonSerializer.Deserialize<T>(stream, this.serializerOptions)!;
        }

        public override Stream ToStream<T>(T input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            MemoryStream stream = new MemoryStream();
            Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
            JsonSerializer.Serialize(writer, input, this.serializerOptions);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
