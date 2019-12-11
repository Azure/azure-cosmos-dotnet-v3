//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;

    internal class CosmosJsonSerializerWrapper : CosmosSerializer
    {
        internal CosmosSerializer InternalJsonSerializer { get; }

        public CosmosJsonSerializerWrapper(CosmosSerializer cosmosJsonSerializer)
        {
            this.InternalJsonSerializer = cosmosJsonSerializer ?? throw new ArgumentNullException(nameof(cosmosJsonSerializer));
        }

        public override async ValueTask<T> FromStreamAsync<T>(
            Stream stream,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            T item = await this.InternalJsonSerializer.FromStreamAsync<T>(stream, cancellationToken);
            if (stream.CanRead)
            {
                throw new InvalidOperationException("Json Serializer left an open stream.");
            }

            return item;
        }

        public override async Task<Stream> ToStreamAsync<T>(
            T input,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Stream stream = await this.InternalJsonSerializer.ToStreamAsync<T>(input, cancellationToken);
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
