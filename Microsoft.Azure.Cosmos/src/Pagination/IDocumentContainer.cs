// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;

    internal interface IDocumentContainer : IMonadicDocumentContainer, IFeedRangeProvider, IQueryDataSource
    {
        Task<Record> CreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken);

        Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            string identifier,
            CancellationToken cancellationToken);

        Task<DocumentContainerPage> ReadFeedAsync(
            int partitionKeyRangeId,
            ResourceIdentifier resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken);

        Task SplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken);
    }
}
