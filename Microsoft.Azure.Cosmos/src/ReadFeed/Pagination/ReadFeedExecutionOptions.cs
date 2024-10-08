// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;

    internal sealed class ReadFeedExecutionOptions : ExecutionOptions
    {
        public static readonly ReadFeedExecutionOptions Default = new ReadFeedExecutionOptions();

        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.Continuation,
            HttpConstants.HttpHeaders.ContinuationToken,
            HttpConstants.HttpHeaders.EnumerationDirection,
        }
        .Concat(ExecutionOptions.bannedAdditionalHeaders)
        .ToImmutableHashSet();

        public ReadFeedExecutionOptions(
            PaginationDirection? paginationDirection = null,
            int? pageSizeHint = null,
            Dictionary<string, string> additionalHeaders = null)
            : base(pageSizeHint, additionalHeaders)
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
