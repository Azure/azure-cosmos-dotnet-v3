//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedObserverFactoryCore : ChangeFeedObserverFactory
    {
        private readonly ChangesStreamHandler onChanges;

        public ChangeFeedObserverFactoryCore(ChangesStreamHandler onChanges)
        {
            this.onChanges = onChanges;
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.onChanges);
        }
    }

    internal sealed class ChangeFeedObserverFactoryCore<T> : ChangeFeedObserverFactory
    {
        private readonly ChangesHandler<T> onChanges;
        private readonly CosmosSerializerCore cosmosSerializer;

        public ChangeFeedObserverFactoryCore(ChangesHandler<T> onChanges, CosmosSerializerCore cosmosSerializer)
        {
            this.onChanges = onChanges ?? throw new ArgumentNullException(nameof(onChanges));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.ChangesStreamHandlerAsync);
        }

        private async Task ChangesStreamHandlerAsync(Stream changes, CancellationToken cancellationToken)
        {
            Collection<T> asFeedResponse;
            try
            {
                asFeedResponse = this.cosmosSerializer.FromStream<CosmosFeedResponseUtil<T>>(changes).Data;
            }
            catch (Exception serializationException)
            {
                // Error using custom serializer to parse stream
                throw new ObserverException(serializationException);
            }

            List<T> asReadOnlyList = new List<T>(asFeedResponse.Count);
            asReadOnlyList.AddRange(asFeedResponse);
            await this.onChanges(asReadOnlyList.AsReadOnly(), cancellationToken);
        }
    }
}