//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Data.Cosmos;
    using Microsoft.Azure.Documents;

    /// <summary> 
    ///  Represents an abstract resource type in the Azure Cosmos DB service.
    ///  All Azure Cosmos DB resources, such as <see cref="Database"/>, <see cref="DocumentCollection"/>, and <see cref="Document"/> extend this abstract type.
    /// </summary>
    internal static class CosmosResource
    {
        private static CosmosTextJsonSerializer cosmosDefaultJsonSerializer = new CosmosTextJsonSerializer();

        internal static ValueTask<T> FromStreamAsync<T>(DocumentServiceResponse response, CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.ResponseBody != null && (!response.ResponseBody.CanSeek || response.ResponseBody.Length > 0))
            {
                return CosmosResource.FromStreamAsync<T>(response.ResponseBody, cancellationToken);
            }

            return new ValueTask<T>(default(T));
        }

        internal static Task<Stream> ToStreamAsync<T>(T input, CancellationToken cancellationToken)
        {
            return CosmosResource.cosmosDefaultJsonSerializer.ToStreamAsync(input, cancellationToken);
        }

        internal static ValueTask<T> FromStreamAsync<T>(Stream stream, CancellationToken cancellationToken)
        {
            return CosmosResource.cosmosDefaultJsonSerializer.FromStreamAsync<T>(stream, cancellationToken);
        }
    }
}
