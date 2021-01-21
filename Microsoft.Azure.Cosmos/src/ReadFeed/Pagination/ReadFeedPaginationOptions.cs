// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class ReadFeedPaginationOptions : PaginationOptions
    {
        public static readonly ReadFeedPaginationOptions Default = new ReadFeedPaginationOptions();

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

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => throw new System.NotImplementedException();

        public enum PaginationDirection
        {
            Forward,
            Reverse,
        }
    }
}
