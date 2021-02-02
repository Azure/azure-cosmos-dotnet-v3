// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>
    {
        private readonly IChangeFeedDataSource changeFeedDataSource;
        private readonly int pageSize;
        private readonly ChangeFeedMode changeFeedMode;
        private readonly JsonSerializationFormat? jsonSerializationFormat;

        public ChangeFeedPartitionRangePageAsyncEnumerator(
            IChangeFeedDataSource changeFeedDataSource,
            FeedRangeInternal range,
            int pageSize,
            ChangeFeedMode changeFeedMode,
            JsonSerializationFormat? jsonSerializationFormat,
            ChangeFeedState state,
            CancellationToken cancellationToken)
            : base(range, cancellationToken, state)
        {
            this.changeFeedDataSource = changeFeedDataSource ?? throw new ArgumentNullException(nameof(changeFeedDataSource));
            this.changeFeedMode = changeFeedMode ?? throw new ArgumentNullException(nameof(changeFeedMode));
            this.pageSize = pageSize;
            this.jsonSerializationFormat = jsonSerializationFormat;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<ChangeFeedPage>> GetNextPageAsync(
            ITrace trace, 
            CancellationToken cancellationToken) => this.changeFeedDataSource.MonadicChangeFeedAsync(
            this.State,
            this.Range,
            this.pageSize,
            this.changeFeedMode,
            this.jsonSerializationFormat,
            trace,
            cancellationToken);
    }
}
