// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal interface IDocumentContainer : IMonadicDocumentContainer, IFeedRangeProvider
    {
        Task<Record> CreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken);

        Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            Guid identifier,
            CancellationToken cancellationToken);

        Task<DocumentContainerPage> ReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken);

        Task SplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken);
    }
}
