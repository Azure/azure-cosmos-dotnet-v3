//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
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
            this.onChanges = onChanges ?? throw new ArgumentNullException(nameof(onChanges));
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.onChanges);
        }
    }

    internal sealed class ChangeFeedObserverFactoryCore<T> : ChangeFeedObserverFactory
    {
        private readonly ChangesHandler<T> legacyOnChanges;
        private readonly ChangeFeedHandler<T> onChanges;
        private readonly ChangeFeedHandlerWithManualCheckpoint<T> onChangesWithManualCheckpoint;
        private readonly CosmosSerializerCore serializerCore;

        public ChangeFeedObserverFactoryCore(
            ChangesHandler<T> onChanges,
            CosmosSerializerCore serializerCore)
            : this(serializerCore)
        {
            this.legacyOnChanges = onChanges ?? throw new ArgumentNullException(nameof(onChanges));
        }

        public ChangeFeedObserverFactoryCore(
            ChangeFeedHandler<T> onChanges,
            CosmosSerializerCore serializerCore)
            : this(serializerCore)
        {
            this.onChanges = onChanges ?? throw new ArgumentNullException(nameof(onChanges));
        }

        public ChangeFeedObserverFactoryCore(
            ChangeFeedHandlerWithManualCheckpoint<T> onChanges,
            CosmosSerializerCore serializerCore)
            : this(serializerCore)
        {
            this.onChangesWithManualCheckpoint = onChanges ?? throw new ArgumentNullException(nameof(onChanges));
        }
        private ChangeFeedObserverFactoryCore(CosmosSerializerCore serializerCore)
        {
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.ChangesStreamHandlerAsync);
        }

        private Task ChangesStreamHandlerAsync(
            ChangeFeedProcessorContext context,
            Stream stream,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<T> changes = this.AsIReadOnlyCollection(stream);
            if (changes.Count == 0)
            {
                return Task.CompletedTask;
            }

            if (this.legacyOnChanges != null)
            {
                return this.legacyOnChanges(changes, cancellationToken);
            }

            if (this.onChanges != null)
            {
                return this.onChanges(context, changes, cancellationToken);
            }

            return this.onChangesWithManualCheckpoint(context as ChangeFeedProcessorContextWithManualCheckpoint, changes, cancellationToken);
        }

        private IReadOnlyCollection<T> AsIReadOnlyCollection(Stream stream)
        {
            try
            {
                return CosmosFeedResponseSerializer.FromFeedResponseStream<T>(
                                    this.serializerCore,
                                    stream);
            }
            catch (Exception serializationException)
            {
                // Error using custom serializer to parse stream
                throw new ObserverException(serializationException);
            }
        }
    }
}