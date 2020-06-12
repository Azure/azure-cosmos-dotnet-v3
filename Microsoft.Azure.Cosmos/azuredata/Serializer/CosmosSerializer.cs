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

            return new CosmosSerializer(new CosmosJsonSerializerWrapper(objectSerializer));
        }

        private readonly Azure.Core.ObjectSerializer objectSerializer;

        /// <summary>
        /// For mocking purposes
        /// </summary>
        internal CosmosSerializer()
        {
        }

        private CosmosSerializer(Azure.Core.ObjectSerializer objectSerializer)
        {
            this.objectSerializer = objectSerializer;
        }

        public virtual T FromStream<T>(Stream stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using (stream)
            {
                return (T)this.objectSerializer.Deserialize(stream, typeof(T));
            }
        }

        public virtual Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            this.objectSerializer.Serialize(streamPayload, input, typeof(T));
            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}
