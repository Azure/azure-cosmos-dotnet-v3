// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;

    internal sealed class ReadFeedPaginationOptions : PaginationOptions
    {
        public static readonly ReadFeedPaginationOptions Default = new ReadFeedPaginationOptions();

        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.Continuation,
            HttpConstants.HttpHeaders.ContinuationToken,
            HttpConstants.HttpHeaders.EnumerationDirection,
        }
        .Concat(PaginationOptions.bannedAdditionalHeaders)
        .ToImmutableHashSet();

        public ReadFeedPaginationOptions(
            PaginationDirection? paginationDirection = null,
            int? pageSizeHint = null,
            JsonSerializationFormat? jsonSerializationFormat = null,
            Dictionary<string, string> additionalHeaders = null)
            : base(pageSizeHint, jsonSerializationFormat, additionalHeaders)
        {
            this.Direction = paginationDirection;
        }

        public PaginationDirection? Direction { get; }

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => BannedHeaders;

        public enum PaginationDirection
        {
            Forward,
            Reverse,
        }
    }
}
