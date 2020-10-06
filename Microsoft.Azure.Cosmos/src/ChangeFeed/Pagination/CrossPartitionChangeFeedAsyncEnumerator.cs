// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal sealed class CrossPartitionChangeFeedAsyncEnumerator : IAsyncEnumerator<TryCatch<ChangeFeedPage>>
    {
        private readonly CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator;
        private readonly CancellationToken cancellationToken;

        private CrossPartitionChangeFeedAsyncEnumerator(
            CrossPartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> crossPartitionEnumerator,
            CancellationToken cancellationToken)
        {
            this.crossPartitionEnumerator = crossPartitionEnumerator ?? throw new ArgumentNullException(nameof(crossPartitionEnumerator));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<ChangeFeedPage> Current { get; private set; }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            if (!await this.crossPartitionEnumerator.MoveNextAsync())
            {
                this.Current = default;
                return false;
            }

            TryCatch<CrossPartitionPage<ChangeFeedPage, ChangeFeedState>> currentCrossPartitionPage = this.crossPartitionEnumerator.Current;
            if (currentCrossPartitionPage.Failed)
            {
                this.Current = TryCatch<ChangeFeedPage>.FromException(currentCrossPartitionPage.Exception);
                return true;
            }

            throw new NotImplementedException();
        }

        public static TryCatch<CrossPartitionChangeFeedAsyncEnumerator> MonadicCreate(
            IDocumentContainer documentContainer,
            ChangeFeedRequestOptions changeFeedRequestOptions,
            ChangeFeedStartFrom changeFeedStartFrom,
            CancellationToken cancellationToken)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (changeFeedRequestOptions == null)
            {
                throw new ArgumentNullException(nameof(changeFeedRequestOptions));
            }

            throw new NotImplementedException();
        }

        //private sealed class CrossPartitionStateAsyncExtractor : ChangeFeedStartFromAsyncVisitor<IDocumentContainer, TryCatch<CrossPartitionState<ChangeFeedState>>>
        //{
        //    public static readonly CrossPartitionStateAsyncExtractor Singleton = new CrossPartitionStateAsyncExtractor();

        //    private CrossPartitionStateAsyncExtractor()
        //    {
        //    }

        //    public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
        //        ChangeFeedStartFromNow startFromNow,
        //        IDocumentContainer documentContainer,
        //        CancellationToken cancellationToken)
        //    {
        //        List<(PartitionKeyRange, ChangeFeedState)> rangeAndStates = new List<(PartitionKeyRange, ChangeFeedState)>();
        //        IReadOnlyList<PartitionKeyRange> ranges;
        //        if (startFromNow.FeedRange != null)
        //        {
        //            ranges = documentContainer.GetChildRangeAsync(
        //                new PartitionKeyRange()
        //                {
        //                    startFromNow.FeedRange.
        //                })
        //        }
        //    }

        //    public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
        //        ChangeFeedStartFromTime startFromTime,
        //        IDocumentContainer documentContainer,
        //        CancellationToken cancellationToken)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
        //        ChangeFeedStartFromContinuation startFromContinuation,
        //        IDocumentContainer documentContainer,
        //        CancellationToken cancellationToken)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
        //        ChangeFeedStartFromBeginning startFromBeginning,
        //        IDocumentContainer documentContainer,
        //        CancellationToken cancellationToken)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override Task<TryCatch<CrossPartitionState<ChangeFeedState>>> VisitAsync(
        //        ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange,
        //        IDocumentContainer documentContainer,
        //        CancellationToken cancellationToken)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    private sealed class FeedRangeToPartitionKeyRanges : IFeedRangeAsyncVisitor<List<PartitionKeyRange>, IDocumentContainer>
        //    {

        //    }
        //}
    }
}
