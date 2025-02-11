// ------------------------------------------------------------
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
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedExecutionOptions : ExecutionOptions
    {
        public static readonly ChangeFeedExecutionOptions Default = new ChangeFeedExecutionOptions(
            mode: ChangeFeedMode.Incremental);

        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.A_IM,
            HttpConstants.HttpHeaders.IfModifiedSince,
            HttpConstants.HttpHeaders.IfNoneMatch,
        }
        .Concat(ExecutionOptions.bannedAdditionalHeaders)
        .ToImmutableHashSet();

        public ChangeFeedExecutionOptions(
            ChangeFeedMode mode,
            int? pageSizeHint = null,
            JsonSerializationFormat? jsonSerializationFormat = null,
            Dictionary<string, string> additionalHeaders = null,
            ChangeFeedQuerySpec changeFeedQuerySpec = null)
            : base(pageSizeHint, additionalHeaders)
        {
            this.Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            this.ChangeFeedQuerySpec = changeFeedQuerySpec;
            this.JsonSerializationFormat = jsonSerializationFormat;
        }

        public ChangeFeedMode Mode { get; }

        public ChangeFeedQuerySpec ChangeFeedQuerySpec { get; }

        public JsonSerializationFormat? JsonSerializationFormat { get; }

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => BannedHeaders;
    }
}
