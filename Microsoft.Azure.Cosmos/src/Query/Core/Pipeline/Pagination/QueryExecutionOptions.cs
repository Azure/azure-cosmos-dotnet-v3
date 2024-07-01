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

    internal sealed class QueryExecutionOptions : ExecutionOptions
    {
        public static readonly QueryExecutionOptions Default = new QueryExecutionOptions();

        public static readonly ImmutableHashSet<string> BannedHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.Continuation,
            HttpConstants.HttpHeaders.ContinuationToken,
            HttpConstants.HttpHeaders.IsQuery,
            HttpConstants.HttpHeaders.IsQueryPlanRequest,
            HttpConstants.HttpHeaders.IsContinuationExpected,
            HttpConstants.HttpHeaders.ContentType,
        }
            .Concat(ExecutionOptions.bannedAdditionalHeaders)
            .ToImmutableHashSet();

        public bool OptimisticDirectExecute { get; }

        public bool EnableDistributedQueryGatewayMode { get; }

        public QueryExecutionOptions(
            int? pageSizeHint = null,
            IReadOnlyDictionary<string, string> additionalHeaders = null,
            bool optimisticDirectExecute = false,
            bool enableDistributedQueryGatewayMode = false)
            : base(pageSizeHint, additionalHeaders)
        {
            this.OptimisticDirectExecute = optimisticDirectExecute;
            this.EnableDistributedQueryGatewayMode = enableDistributedQueryGatewayMode;
        }

        protected override ImmutableHashSet<string> BannedAdditionalHeaders => BannedHeaders;
    }
}
