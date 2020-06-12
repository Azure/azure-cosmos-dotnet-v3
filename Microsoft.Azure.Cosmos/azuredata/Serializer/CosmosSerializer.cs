//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Serialization
{
    using System;
    using System.IO;

    /// <summary>
    /// Wraps an <see cref="Azure.Core.ObjectSerializer"/> into known methods.
    /// </summary>
    internal class CosmosSerializer
    {
        public static CosmosSerializer ForObjectSerializer(Azure.Core.ObjectSerializer objectSerializer)
        {
            if (objectSerializer == null)
            {
                throw new ArgumentNullException(nameof(objectSerializer));
            }

            return new CosmosSerializer(objectSerializer);
        }

        internal readonly Azure.Core.ObjectSerializer AzureCoreSerializer;

        /// <summary>
        /// For mocking purposes
        /// </summary>
        internal CosmosSerializer()
        {
        }

        private CosmosSerializer(Azure.Core.ObjectSerializer objectSerializer)
        {
            this.AzureCoreSerializer = objectSerializer;
        }

        public virtual T FromStream<T>(Stream stream)
        {
            if (stream.CanSeek
                    && stream.Length == 0)
            {
                return default(T);
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return (T)this.AzureCoreSerializer.Deserialize(stream, typeof(T));
        }

        public virtual Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            this.AzureCoreSerializer.Serialize(streamPayload, input, typeof(T));
            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}
