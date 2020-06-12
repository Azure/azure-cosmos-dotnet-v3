//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    internal class CosmosJsonSerializerWrapper : Azure.Core.ObjectSerializer
    {
        internal Azure.Core.ObjectSerializer InternalJsonSerializer { get; }

        public CosmosJsonSerializerWrapper(Azure.Core.ObjectSerializer cosmosJsonSerializer)
        {
            this.InternalJsonSerializer = cosmosJsonSerializer ?? throw new ArgumentNullException(nameof(cosmosJsonSerializer));
        }

        public override void Serialize(Stream stream, object value, Type inputType)
        {
            this.InternalJsonSerializer.Serialize(stream, value, inputType);
            if (stream == null)
            {
                throw new InvalidOperationException("Json Serializer returned a null stream.");
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer returned a closed stream.");
            }
        }

        public override async ValueTask SerializeAsync(Stream stream, object value, Type inputType)
        {
            await this.InternalJsonSerializer.SerializeAsync(stream, value, inputType);
            if (stream == null)
            {
                throw new InvalidOperationException("Json Serializer returned a null stream.");
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer returned a closed stream.");
            }
        }

        public override object Deserialize(Stream stream, Type returnType)
        {
            object item = this.InternalJsonSerializer.Deserialize(stream, returnType);
            if (stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer left an open stream.");
            }

            return item;
        }

        public override async ValueTask<object> DeserializeAsync(Stream stream, Type returnType)
        {
            object item = await this.InternalJsonSerializer.DeserializeAsync(stream, returnType);
            if (stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer left an open stream.");
            }

            return item;
        }
    }
}
