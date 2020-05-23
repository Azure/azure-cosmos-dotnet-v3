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
        private static readonly JsonSerializer serializer = JsonSerializer.Create();
        private const int DefaultCapacity = 1024;

        public static string ToString<T>(T input)
        {
            MemoryStream stream = JsonHelper.ToStream(input);
            using (StreamReader sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }

        public static T Deserialize<T>(string payload)
        {
            return JsonConvert.DeserializeObject<T>(payload);
        }

        public static MemoryStream ToStream<T>(T input)
        {
            MemoryStream memStreamPayload = new MemoryStream(JsonHelper.DefaultCapacity);
            using (StreamWriter streamWriter = new StreamWriter(memStreamPayload,
                encoding: JsonHelper.DefaultEncoding,
                bufferSize: JsonHelper.DefaultCapacity,
                leaveOpen: true))
            {
                using (JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Formatting.None;
                    JsonSerializer jsonSerializer = JsonHelper.serializer;
                    jsonSerializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            memStreamPayload.Position = 0;
            return memStreamPayload;
        }
    }
}
