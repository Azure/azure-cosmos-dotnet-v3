//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal sealed class InMemoryCollectionPartitionRangeEnumerator : PartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState>
    {
        private readonly InMemoryCollection inMemoryCollection;
        private readonly int pageSize;
        private readonly int partitionKeyRangeId;

        public InMemoryCollectionPartitionRangeEnumerator(
            InMemoryCollection inMemoryCollection,
            int partitionKeyRangeId,
            int pageSize,
            InMemoryCollectionState state = null)
            : base(
                  new PartitionKeyRange()
                  {
                      Id = partitionKeyRangeId.ToString(),
                      MinInclusive = partitionKeyRangeId.ToString(),
                      MaxExclusive  = partitionKeyRangeId.ToString()
                  },
                  state ?? new InMemoryCollectionState(resourceIdentifier: 0))
        {
            this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
            this.partitionKeyRangeId = partitionKeyRangeId;
            this.pageSize = pageSize;
        }

        public override ValueTask DisposeAsync() => default;

        public override Task<TryCatch<InMemoryCollectionPage>> GetNextPageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<(List<InMemoryCollection.Record> records, long? continuation)> tryReadPage = this.inMemoryCollection.ReadFeed(
                partitionKeyRangeId: this.partitionKeyRangeId,
                resourceIndentifer: ((InMemoryCollectionState)this.State).ResourceIdentifier,
                pageSize: this.pageSize);

            if (tryReadPage.Failed)
            {
                return Task.FromResult(TryCatch<InMemoryCollectionPage>.FromException(tryReadPage.Exception));
            }

            InMemoryCollectionState inMemoryCollectionState = tryReadPage.Result.continuation.HasValue ? new InMemoryCollectionState(tryReadPage.Result.continuation.Value) : default;
            InMemoryCollectionPage page = new InMemoryCollectionPage(tryReadPage.Result.records, inMemoryCollectionState);

            return Task.FromResult(TryCatch<InMemoryCollectionPage>.FromResult(page));
        }
    }
}
