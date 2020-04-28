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
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Internal feed iterator API for casting and mocking purposes.
    /// </summary>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class FeedIteratorInternal : FeedIterator
    {
        public abstract CosmosElement GetCosmsoElementContinuationToken();

        internal virtual async Task<Stream> GetTransformedResponseMessageAsync(
            Stream content,
            CosmosSerializerCore cosmosSerializerCore,
            CosmosStreamTransformer cosmosStreamTransformer,
            string containerRid,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            CosmosArray cosmosArray = CosmosElementSerializer.ToCosmosElements(
                    content,
                    Documents.ResourceType.Document);

            List<CosmosElement> transformedCosmosElements = await this.GetTransformedElementResponseAsync(
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

        internal async Task<List<CosmosElement>> GetTransformedElementResponseAsync(
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
                        null,
                        cancellationToken);

                    CosmosElement transformedCosmosElement = cosmosSerializerCore.FromStream<CosmosElement>(transformedContent);
                    transformedCosmosElements.Add(transformedCosmosElement);
                }
            }

            return transformedCosmosElements;
        }

        internal async Task<TryCatch<string>> TryInitializeContainerRIdAsync(ContainerInternal container, CancellationToken cancellationToken)
        {
            try
            {
                string containerRId = await container.GetRIDAsync(cancellationToken);
                return TryCatch<string>.FromResult(containerRId);
            }
            catch (CosmosException cosmosException)
            {
                return TryCatch<string>.FromException(cosmosException);
            }
        }
    }
}