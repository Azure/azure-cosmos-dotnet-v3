//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System;

    /// <summary>
    /// Factory class used to create instance(s) of <see cref="ChangeFeedObserver{T}"/>.
    /// </summary>
    internal sealed class CheckpointerObserverFactory<T> : ChangeFeedObserverFactory<T>
    {
        private readonly ChangeFeedObserverFactory<T> observerFactory;
        private readonly CheckpointFrequency checkpointFrequency;

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointerObserverFactory{T}"/> class.
        /// </summary>
        /// <param name="observerFactory">Instance of Observer Factory</param>
        /// <param name="checkpointFrequency">Defined <see cref="CheckpointFrequency"/></param>
        public CheckpointerObserverFactory(ChangeFeedObserverFactory<T> observerFactory, CheckpointFrequency checkpointFrequency)
        {
            if (observerFactory == null)
                throw new ArgumentNullException(nameof(observerFactory));
            if (checkpointFrequency == null)
                throw new ArgumentNullException(nameof(checkpointFrequency));

            this.observerFactory = observerFactory;
            this.checkpointFrequency = checkpointFrequency;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ChangeFeedObserver{T}"/>.
        /// </summary>
        /// <returns>Created instance of <see cref="ChangeFeedObserver{T}"/>.</returns>
        public override ChangeFeedObserver<T> CreateObserver()
        {
            ChangeFeedObserver<T> observer = new ObserverExceptionWrappingChangeFeedObserverDecorator<T>(this.observerFactory.CreateObserver());
            if (this.checkpointFrequency.ExplicitCheckpoint) return observer;

            return new AutoCheckpointer<T>(this.checkpointFrequency, observer);
        }
    }
}