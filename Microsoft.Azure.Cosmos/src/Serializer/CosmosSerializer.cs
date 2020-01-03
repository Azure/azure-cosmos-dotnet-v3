//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This is an interface to allow a custom serializer to be used by the CosmosClient
    /// </summary>
    public abstract class CosmosSerializer
    {
        /// <summary>
        /// Convert a Stream of JSON to an object. 
        /// The implementation is responsible for Disposing of the stream,
        /// including when an exception is thrown, to avoid memory leaks.
        /// </summary>
        /// <typeparam name="T">Any type passed to <see cref="Container"/>.</typeparam>
        /// <param name="stream">The Stream response containing JSON from Cosmos DB.</param>
        /// <returns>The object deserialized from the stream.</returns>
        public abstract T FromStream<T>(Stream stream);

        /// <summary>
        /// Convert the object to a Stream. 
        /// The caller will take ownership of the stream and ensure it is correctly disposed of.
        /// <see href="https://docs.microsoft.com/dotnet/api/system.io.stream.canread">Stream.CanRead</see> must be true.
        /// </summary>
        /// <param name="input">Any type passed to <see cref="Container"/>.</param>
        /// <returns>A readable Stream containing JSON of the serialized object.</returns>
        public abstract Stream ToStream<T>(T input);

        /// <summary>
        /// Convert a Stream of JSON to an object. 
        /// The implementation is responsible for Disposing of the stream,
        /// including when an exception is thrown, to avoid memory leaks.
        /// </summary>
        /// <typeparam name="T">Any type passed to <see cref="Container"/>.</typeparam>
        /// <param name="stream">The Stream response containing JSON from Cosmos DB.</param>
        /// <param name="container">Container that the item is in.</param>
        /// <param name="itemRequestOptions">Request options for the item request.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of the operation.</param>
        /// <returns>The object deserialized from the stream.</returns>
        public virtual Task<T> FromStreamAsync<T>(Stream stream, Container container, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.FromStream<T>(stream));
        }

        /// <summary>
        /// Convert the object to a Stream. 
        /// The caller will take ownership of the stream and ensure it is correctly disposed of.
        /// <see href="https://docs.microsoft.com/dotnet/api/system.io.stream.canread">Stream.CanRead</see> must be true.
        /// </summary>
        /// <param name="input">Any type passed to <see cref="Container"/>.</param>
        /// <param name="container">Container that the item is in.</param>
        /// <param name="itemRequestOptions">Request options for the item request.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of the operation.</param>
        /// <returns>A readable Stream containing JSON of the serialized object.</returns>
        public virtual Task<Stream> ToStreamAsync<T>(T input, Container container, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.ToStream<T>(input));
        }
    }
}
