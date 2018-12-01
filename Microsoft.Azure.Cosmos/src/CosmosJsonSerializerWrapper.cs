//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;

    internal class CosmosJsonSerializerWrapper: CosmosJsonSerializer
    {
        private CosmosJsonSerializer internalJsonSerializer;

        public CosmosJsonSerializerWrapper(CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.internalJsonSerializer = cosmosJsonSerializer ?? throw new ArgumentNullException(nameof(cosmosJsonSerializer));
        }

        public override T FromStream<T>(Stream stream)
        {
            T item = this.internalJsonSerializer.FromStream<T>(stream);
            if (stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer left an open stream.");
            }

            return item;
        }

        public override Stream ToStream<T>(T input)
        {
            Stream stream = this.internalJsonSerializer.ToStream<T>(input);
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
