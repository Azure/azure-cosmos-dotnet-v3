// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;

    internal sealed class QueryPaginationOptions : PaginationOptions
    {
        public static readonly QueryPaginationOptions Default = new QueryPaginationOptions();

        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.Continuation,
            HttpConstants.HttpHeaders.ContinuationToken,
            HttpConstants.HttpHeaders.IsQuery,
            HttpConstants.HttpHeaders.IsQueryPlanRequest,
            HttpConstants.HttpHeaders.IsContinuationExpected,
            HttpConstants.HttpHeaders.ContentType,
        }
        .Concat(PaginationOptions.bannedAdditionalHeaders)
        .ToImmutableHashSet();

        public QueryPaginationOptions(
            int? pageSizeHint = null,
            JsonSerializationFormat? jsonSerializationFormat = null,
            Dictionary<string, string> additionalHeaders = null)
            : base(pageSizeHint, jsonSerializationFormat, additionalHeaders)
        {
        }

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => BannedHeaders;
    }
}
