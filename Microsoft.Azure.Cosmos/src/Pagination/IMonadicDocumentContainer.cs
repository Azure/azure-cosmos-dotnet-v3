// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal interface IMonadicDocumentContainer : IMonadicFeedRangeProvider
    {
        Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken);

        Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            Guid identifer,
            CancellationToken cancellationToken);

        Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken);

        Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken);
    }
}
