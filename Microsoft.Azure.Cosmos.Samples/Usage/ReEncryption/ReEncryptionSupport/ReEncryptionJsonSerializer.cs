//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class ReEncryptionJsonSerializer
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        private readonly JsonSerializerSettings serializerSettings;

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        internal ReEncryptionJsonSerializer(JsonSerializerSettings jsonSerializerSettings = null)
        {
            this.serializerSettings = jsonSerializerSettings;
        }

        /// <summary>
        /// Convert a Stream to the passed in type.
        /// </summary>
        /// <typeparam name="T">The type of object that should be deserialized</typeparam>
        /// <param name="stream">An open stream that is readable that contains JSON</param>
        /// <returns>The object representing the deserialized stream</returns>
        public T FromStream<T>(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using (StreamReader sr = new StreamReader(stream))
            using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
            {
                JsonSerializer jsonSerializer = this.GetSerializer();
                return jsonSerializer.Deserialize<T>(jsonTextReader);
            }
        }

        /// <summary>
        /// Converts an object to a open readable stream.
        /// </summary>
        /// <typeparam name="T">The type of object being serialized</typeparam>
        /// <param name="input">The object to be serialized</param>
        /// <returns>An open readable stream containing the JSON of the serialized object</returns>
        public MemoryStream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: ReEncryptionJsonSerializer.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
            using (JsonWriter writer = new JsonTextWriter(streamWriter))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                JsonSerializer jsonSerializer = this.GetSerializer();
                jsonSerializer.Serialize(writer, input);
                writer.Flush();
                streamWriter.Flush();
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        private JsonSerializer GetSerializer()
        {
            return JsonSerializer.Create(this.serializerSettings);
        }
    }
}