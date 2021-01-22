// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;

    internal sealed class OrderByQueryPage : Page<QueryState>
    {
        private static readonly ImmutableHashSet<string> bannedHeaders = new HashSet<string>()
        {
            Microsoft.Azure.Documents.HttpConstants.HttpHeaders.Continuation,
            Microsoft.Azure.Documents.HttpConstants.HttpHeaders.ContinuationToken,
        }.ToImmutableHashSet();

        public OrderByQueryPage(QueryPage queryPage)
            : base(queryPage.RequestCharge, queryPage.ActivityId, queryPage.AdditionalHeaders, queryPage.State)
        {
            this.Page = queryPage ?? throw new ArgumentNullException(nameof(queryPage));
            this.Enumerator = queryPage.Documents.GetEnumerator();
        }

        public QueryPage Page { get; }

        public IEnumerator<CosmosElement> Enumerator { get; }

        protected override ImmutableHashSet<string> DerivedClassBannedHeaders => bannedHeaders;
    }
}
