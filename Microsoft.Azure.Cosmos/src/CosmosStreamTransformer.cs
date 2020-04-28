//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stream transformer that transforms the request stream before writing to server and the response stream before returning to user.
    /// </summary>
    public abstract class CosmosStreamTransformer
    {
        /// <summary>
        /// Transforms the request stream before writing to server
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Stream</returns>
        public abstract Task<Stream> TransformRequestItemStreamAsync(Stream input, StreamTransformationContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Transforms the response stream before returning to user
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Stream</returns>
        public abstract Task<Stream> TransformResponseItemStreamAsync(Stream input, StreamTransformationContext context, CancellationToken cancellationToken);

    }
}
