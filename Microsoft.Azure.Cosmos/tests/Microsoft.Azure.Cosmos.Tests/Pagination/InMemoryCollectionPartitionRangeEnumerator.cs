//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;

    internal sealed class InMemoryCollectionPartitionRangeEnumerator : PartitionRangePageEnumerator
    {
        private readonly InMemoryCollection inMemoryCollection;
        private readonly int pageSize;
        private readonly int partitionKeyRangeId;

        public InMemoryCollectionPartitionRangeEnumerator(InMemoryCollection inMemoryCollection, int partitionKeyRangeId, int pageSize, State state = null)
            : base(new FeedRangePartitionKeyRange(partitionKeyRangeId.ToString()), state ?? new InMemoryCollectionState(resourceIdentifier: 0))
        {
            this.inMemoryCollection = inMemoryCollection ?? throw new ArgumentNullException(nameof(inMemoryCollection));
            this.partitionKeyRangeId = partitionKeyRangeId;
            this.pageSize = pageSize;

            if (state != null)
            {
                if (!(state is InMemoryCollectionState _))
                {
                    throw new ArgumentOutOfRangeException(nameof(state));
                }
            }
        }

        public override ValueTask DisposeAsync()
        {
            // Do Nothing
            return default;
        }

        public override Task<TryCatch<Page>> GetNextPageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<(List<InMemoryCollection.Record> records, long? continuation)> tryReadPage = this.inMemoryCollection.ReadFeed(
                partitionKeyRangeId: this.partitionKeyRangeId,
                resourceIndentifer: ((InMemoryCollectionState)this.State).ResourceIdentifier,
                pageSize: this.pageSize);

            if (tryReadPage.Failed)
            {
                return Task.FromResult(TryCatch<Page>.FromException(tryReadPage.Exception));
            }

            State inMemoryCollectionState = tryReadPage.Result.continuation.HasValue ? new InMemoryCollectionState(tryReadPage.Result.continuation.Value) : default;
            InMemoryCollectionPage page = new InMemoryCollectionPage(tryReadPage.Result.records, inMemoryCollectionState);

            return Task.FromResult(TryCatch<Page>.FromResult(page));
        }

        private sealed class InMemoryCollectionState : State
        {
            public InMemoryCollectionState(long resourceIdentifier)
            {
                this.ResourceIdentifier = resourceIdentifier;
            }

            public long ResourceIdentifier { get; }
        }

        public sealed class InMemoryCollectionPage : Page
        {
            public InMemoryCollectionPage(List<InMemoryCollection.Record> records, State state)
                : base(state)
            {
                this.Records = records;
            }

            public List<InMemoryCollection.Record> Records { get; }
        }
    }
}
