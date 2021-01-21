// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class QueryPaginationOptions : PaginationOptions
    {
        public static readonly QueryPaginationOptions Default = new QueryPaginationOptions();

        public QueryPaginationOptions(
            int? pageSizeHint = null,
            JsonSerializationFormat? jsonSerializationFormat = null,
            Dictionary<string, string> additionalHeaders = null)
            : base(pageSizeHint, jsonSerializationFormat, additionalHeaders)
        {
        }

        private static readonly ImmutableHashSet<string> bannedAdditionalHeaders = new HashSet<string>().ToImmutableHashSet();

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => bannedAdditionalHeaders;
    }
}
