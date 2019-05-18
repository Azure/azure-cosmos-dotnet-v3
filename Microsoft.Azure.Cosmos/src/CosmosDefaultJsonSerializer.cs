//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// The default Cosmos JSON serializer
    /// </summary>
    internal class CosmosDefaultJsonSerializer : CosmosJsonSerializer
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializer Serializer = new JsonSerializer()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public override T FromStream<T>(Stream stream)
        {
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
                        return CosmosDefaultJsonSerializer.Serializer.Deserialize<T>(jsonTextReader);
                    }
                }
            }
        }
        
        public override Stream ToStream<T>(T input)
        {
            return this.ToStream<T>(input, null, out object partitionKey);
        }

        public Stream ToStream<T>(T input, IList<string> partitionKeyPathTokens, out object partitionKey)
        {
            MemoryStream streamPayload = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: CosmosDefaultJsonSerializer.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
            {                
                using (PartitionKeyIntercepterJsonTextWriter writer = new PartitionKeyIntercepterJsonTextWriter(streamWriter, partitionKeyPathTokens))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    CosmosDefaultJsonSerializer.Serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();                    
                    partitionKey = writer.PartitionKey;
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}
