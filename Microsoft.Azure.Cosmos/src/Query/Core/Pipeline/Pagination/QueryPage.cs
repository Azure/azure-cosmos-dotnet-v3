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
            long responseLengthInBytes,
            CosmosQueryExecutionInfo cosmosQueryExecutionInfo,
            string disallowContinuationTokenMessage,
            IReadOnlyDictionary<string, string> additionalHeaders,
            QueryState state)
            : base(requestCharge, activityId, additionalHeaders, state)
        {
            this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
            this.ResponseLengthInBytes = responseLengthInBytes < 0 ? throw new ArgumentOutOfRangeException(nameof(responseLengthInBytes)) : responseLengthInBytes;
            this.CosmosQueryExecutionInfo = cosmosQueryExecutionInfo;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
        }

        public IReadOnlyList<CosmosElement> Documents { get; }

        public long ResponseLengthInBytes { get; }

        public CosmosQueryExecutionInfo CosmosQueryExecutionInfo { get; }

        public string DisallowContinuationTokenMessage { get; }

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => QueryPage.BannedHeaders;
    }
}