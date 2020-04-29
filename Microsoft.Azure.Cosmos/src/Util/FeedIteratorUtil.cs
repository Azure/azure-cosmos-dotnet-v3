//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal static class FeedIteratorUtil
    {
        internal static async Task<Stream> GetTransformedResponseMessageAsync(
            Stream content,
            CosmosSerializerCore cosmosSerializerCore,
            CosmosStreamTransformer cosmosStreamTransformer,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            (CosmosArray cosmosArray, string containerRid) = CosmosElementSerializer.ToCosmosElements(
                    content,
                    Documents.ResourceType.Document);

            List<CosmosElement> transformedCosmosElements = await FeedIteratorUtil.GetTransformedElementResponseAsync(
                cosmosSerializerCore,
                cosmosStreamTransformer,
                cosmosArray,
                diagnosticsContext,
                cancellationToken);

            return CosmosElementSerializer.ToStream(
                containerRid,
                transformedCosmosElements,
                Documents.ResourceType.Document);
        }

        internal static async Task<List<CosmosElement>> GetTransformedElementResponseAsync(
            CosmosSerializerCore cosmosSerializerCore,
            CosmosStreamTransformer cosmosStreamTransformer,
            IReadOnlyList<CosmosElement> cosmosElements,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            List<CosmosElement> transformedCosmosElements = new List<CosmosElement>();
            using (diagnosticsContext.CreateScope("Transform"))
            {
                foreach (CosmosElement document in cosmosElements)
                {
                    Stream transformedContent = await cosmosStreamTransformer.TransformResponseItemStreamAsync(
                        CosmosElementSerializer.ElementToMemoryStream(document),
                        context: null,
                        cancellationToken);

                    CosmosElement transformedCosmosElement = cosmosSerializerCore.FromStream<CosmosElement>(transformedContent);
                    transformedCosmosElements.Add(transformedCosmosElement);
                }
            }

            return transformedCosmosElements;
        }
    }
}
