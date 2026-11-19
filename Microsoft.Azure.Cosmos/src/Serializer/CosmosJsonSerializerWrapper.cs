//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;

    internal class CosmosJsonSerializerWrapper : CosmosSerializer
    {
        private readonly bool serializeAsText;

        internal CosmosSerializer InternalJsonSerializer { get; }

        public CosmosJsonSerializerWrapper(CosmosSerializer cosmosJsonSerializer, bool serializeAsText = false)
        {
            this.InternalJsonSerializer = cosmosJsonSerializer ?? throw new ArgumentNullException(nameof(cosmosJsonSerializer));
            this.serializeAsText = serializeAsText;
        }

        public override T FromStream<T>(Stream stream)
        {
            T item = this.InternalJsonSerializer.FromStream<T>(stream);
            if (stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer left an open stream.");
            }

            return item;
        }

        public override Stream ToStream<T>(T input)
        {
            Stream stream = this.serializeAsText
                ? CosmosSerializationUtil.ToStreamForTextJson(this.InternalJsonSerializer, input)
                : this.InternalJsonSerializer.ToStream<T>(input);
            if (stream == null)
            {
                throw new InvalidOperationException("Json Serializer returned a null stream.");
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer returned a closed stream.");
            }

            return stream;
        }
    }
}
