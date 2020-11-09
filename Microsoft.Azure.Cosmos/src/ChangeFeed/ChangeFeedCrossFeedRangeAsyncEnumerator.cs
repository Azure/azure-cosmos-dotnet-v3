// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class ChangeFeedCrossFeedRangeAsyncEnumerator : IAsyncEnumerator<TryCatch<ChangeFeedPage>>
    {
        private readonly CrossPartitionChangeFeedAsyncEnumerator enumerator;

        public ChangeFeedCrossFeedRangeAsyncEnumerator(CrossPartitionChangeFeedAsyncEnumerator enumerator)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        }

        public TryCatch<ChangeFeedPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!await this.enumerator.MoveNextAsync())
            {
                throw new InvalidOperationException("Change Feed should always be able to move next.");
            }

            TryCatch<CrossFeedRangePage<Pagination.ChangeFeedPage, ChangeFeedState>> monadicInnerChangeFeedPage = this.enumerator.Current;
            if (monadicInnerChangeFeedPage.Failed)
            {
                this.Current = TryCatch<ChangeFeedPage>.FromException(monadicInnerChangeFeedPage.Exception);
                return true;
            }

            CrossFeedRangePage<Pagination.ChangeFeedPage, ChangeFeedState> innerChangeFeedPage = monadicInnerChangeFeedPage.Result;
            CrossFeedRangeState<ChangeFeedState> crossFeedRangeState = innerChangeFeedPage.State;
            ChangeFeedCrossFeedRangeState state = new ChangeFeedCrossFeedRangeState(crossFeedRangeState.Value);
            ChangeFeedPage page = innerChangeFeedPage.Page switch
            {
                Pagination.ChangeFeedSuccessPage successPage => new ChangeFeedSuccessPage(
                    CosmosQueryClientCore.ParseElementsFromRestStream(
                        successPage.Content, 
                        Documents.ResourceType.Document, 
                        cosmosSerializationOptions: null),
                    successPage.RequestCharge,
                    successPage.ActivityId,
                    state),
                Pagination.ChangeFeedNotModifiedPage notModifiedPage => new ChangeFeedNotModifiedPage(
                     notModifiedPage.RequestCharge,
                     notModifiedPage.ActivityId,
                     state),
                _ => throw new InvalidOperationException($"Unknown type: {innerChangeFeedPage.Page.GetType()}"),
            };

            this.Current = TryCatch<ChangeFeedPage>.FromResult(page);
            return true;
        }
    }
}
