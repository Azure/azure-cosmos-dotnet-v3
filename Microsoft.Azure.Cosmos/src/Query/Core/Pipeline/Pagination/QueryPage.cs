// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class QueryPage : Page<QueryState>
    {
        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            Microsoft.Azure.Documents.HttpConstants.HttpHeaders.Continuation,
            Microsoft.Azure.Documents.HttpConstants.HttpHeaders.ContinuationToken,
        }.Concat(BannedHeadersBase).ToImmutableHashSet<string>();

        public QueryPage(
            IReadOnlyList<CosmosElement> documents,
            double requestCharge,
            string activityId,
            Lazy<CosmosQueryExecutionInfo> cosmosQueryExecutionInfo,
            DistributionPlanSpec distributionPlanSpec,
            string disallowContinuationTokenMessage,
            IReadOnlyDictionary<string, string> additionalHeaders,
            QueryState state,
            bool? streaming)
            : base(requestCharge, activityId, additionalHeaders, state)
        {
            this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
            this.CosmosQueryExecutionInfo = cosmosQueryExecutionInfo;
            this.DistributionPlanSpec = distributionPlanSpec;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.Streaming = streaming;
        }

        public IReadOnlyList<CosmosElement> Documents { get; }

        public Lazy<CosmosQueryExecutionInfo> CosmosQueryExecutionInfo { get; }

        public DistributionPlanSpec DistributionPlanSpec { get; }

        public string DisallowContinuationTokenMessage { get; }

        public bool? Streaming { get; }

        public override int ItemCount => this.Documents.Count;

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => QueryPage.BannedHeaders;
    }
}