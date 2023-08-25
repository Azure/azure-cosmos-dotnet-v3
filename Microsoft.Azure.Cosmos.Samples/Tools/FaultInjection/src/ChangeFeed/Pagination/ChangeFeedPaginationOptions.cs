﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedPaginationOptions : PaginationOptions
    {
        public static readonly ChangeFeedPaginationOptions Default = new ChangeFeedPaginationOptions(
            mode: ChangeFeedMode.Incremental);

        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.A_IM,
            HttpConstants.HttpHeaders.IfModifiedSince,
            HttpConstants.HttpHeaders.IfNoneMatch,
        }
        .Concat(PaginationOptions.bannedAdditionalHeaders)
        .ToImmutableHashSet();

        public ChangeFeedPaginationOptions(
            ChangeFeedMode mode,
            int? pageSizeHint = null,
            JsonSerializationFormat? jsonSerializationFormat = null,
            Dictionary<string, string> additionalHeaders = null,
            ChangeFeedQuerySpec changeFeedQuerySpec = null)
            : base(pageSizeHint, jsonSerializationFormat, additionalHeaders)
        {
            this.Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            this.ChangeFeedQuerySpec = changeFeedQuerySpec;
        }

        public ChangeFeedMode Mode { get; }

        public ChangeFeedQuerySpec ChangeFeedQuerySpec { get; }

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => BannedHeaders;
    }
}
