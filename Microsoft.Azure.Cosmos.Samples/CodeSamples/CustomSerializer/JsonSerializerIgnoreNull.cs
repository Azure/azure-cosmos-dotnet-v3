namespace Cosmos.Samples.Shared
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    class JsonSerializerIgnoreNull : CosmosSerializer
    {
        public int FromCount = 0;
        public int ToCount = 0;
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializer Serializer = new JsonSerializer()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public override T FromStream<T>(Stream stream)
        {
            FromCount++;
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)(stream);
                }

                using (StreamReader sr = new StreamReader(stream))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                    {
                        return JsonSerializerIgnoreNull.Serializer.Deserialize<T>(jsonTextReader);
                    }
                }
            }
        }

        public override Stream ToStream<T>(T input)
        {
            ToCount++;
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(
                streamPayload,
                encoding: JsonSerializerIgnoreNull.DefaultEncoding,
                bufferSize: 1024,
                leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    JsonSerializerIgnoreNull.Serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}
