//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    internal static class JsonHelper
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings() {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                });
        private const int DefaultCapacity = 1024;

        public static string ToString<T>(T input, int capacity = JsonHelper.DefaultCapacity)
        {
            using (MemoryStream stream = JsonHelper.ToStream(input, capacity))
            using (StreamReader sr = new StreamReader(stream))
            {
                string str = sr.ReadToEnd();
                System.Buffers.ArrayPool<byte>.Shared.Return(stream.GetBuffer());
                
                return str;
            }
        }

        public static T Deserialize<T>(string payload)
        {
            return JsonConvert.DeserializeObject<T>(payload);
        }

        public static MemoryStream ToStream<T>(T input, int capacity = JsonHelper.DefaultCapacity)
        {
            byte[] blob = System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
            MemoryStream memStreamPayload = new MemoryStream(blob, 0, capacity, writable: true, publiclyVisible: true);
            memStreamPayload.SetLength(0);
            memStreamPayload.Position = 0;
            using (StreamWriter streamWriter = new StreamWriter(memStreamPayload,
                encoding: JsonHelper.DefaultEncoding,
                bufferSize: capacity,
                leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    JsonHelper.serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            memStreamPayload.Position = 0;
            return memStreamPayload;
        }
    }
}
