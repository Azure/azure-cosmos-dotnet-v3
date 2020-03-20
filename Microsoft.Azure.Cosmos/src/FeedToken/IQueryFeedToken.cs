// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;

    internal interface IQueryFeedToken
    {
        void EnrichRequest(RequestMessage request);

        string GetContinuation();

        void UpdateContinuation(string continuationToken);

        bool IsDone { get; }

        Task<List<Documents.Routing.Range<string>>> GetAffectedRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition);

        Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken);

        TryCatch ValidateContainer(string containerRid);

        Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken);

        IReadOnlyList<FeedTokenEPKRange> Scale();
    }
}
