// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    internal static class DisallowContinuationTokenMessages
    {
        public const string HybridSearch = "Continuation tokens are not supported by hybrid search.";
        public const string NonStreamingOrderBy = "Continuation tokens are not supported for the non streaming order by pipeline.";
        public const string Distinct = "DISTINCT queries only return continuation tokens when there is a matching ORDER BY clause." +
            "For example if your query is 'SELECT DISTINCT VALUE c.name FROM c', then rewrite it as 'SELECT DISTINCT VALUE c.name FROM c ORDER BY c.name'.";
    }
}