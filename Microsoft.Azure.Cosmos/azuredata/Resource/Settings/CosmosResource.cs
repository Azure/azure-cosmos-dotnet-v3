//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary> 
    ///  Represents an abstract resource type in the Azure Cosmos DB service.
    ///  All Azure Cosmos DB resources, such as <see cref="CosmosDatabase"/>, <see cref="DocumentCollection"/>, and <see cref="Document"/> extend this abstract type.
    /// </summary>
    internal static class CosmosResource
    {
        private static CosmosTextJsonSerializer cosmosDefaultJsonSerializer = new CosmosTextJsonSerializer();

        internal static async Task<T> FromStreamAsync<T>(
            DocumentServiceResponse response,
            CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.ResponseBody != null && (!response.ResponseBody.CanSeek || response.ResponseBody.Length > 0))
            {
                return await CosmosResource.FromStreamAsync<T>(response.ResponseBody, cancellationToken);
            }

            return default(T);
        }

        internal static Task<Stream> ToStreamAsync<T>(
            T input,
            CancellationToken cancellationToken)
        {
            return CosmosResource.cosmosDefaultJsonSerializer.ToStreamAsync(input, cancellationToken);
        }

        internal static ValueTask<T> FromStreamAsync<T>(
            Stream stream,
            CancellationToken cancellationToken)
        {
            return CosmosResource.cosmosDefaultJsonSerializer.FromStreamAsync<T>(stream, cancellationToken);
        }
    }
}
