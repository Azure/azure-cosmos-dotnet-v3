//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;

    /// <summary>
    /// Factory class used to create instance(s) of <see cref="ChangeFeedObserver"/> wrapping with the desired checkpoint logic.
    /// </summary>
    internal sealed class CheckpointerObserverFactory : ChangeFeedObserverFactory
    {
        private readonly ChangeFeedObserverFactory observerFactory;
        private readonly bool withManualCheckpointing;

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointerObserverFactory"/> class.
        /// </summary>
        /// <param name="observerFactory">Instance of Observer Factory</param>
        /// <param name="withManualCheckpointing">Should it automatically checkpoint or not.</param>
        public CheckpointerObserverFactory(
            ChangeFeedObserverFactory observerFactory, 
            bool withManualCheckpointing)
        {
            this.observerFactory = observerFactory ?? throw new ArgumentNullException(nameof(observerFactory));
            this.withManualCheckpointing = withManualCheckpointing;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ChangeFeedObserver"/> with either automatic checkpoint or manual.
        /// </summary>
        /// <returns>Created instance of <see cref="ChangeFeedObserver"/>.</returns>
        public override ChangeFeedObserver CreateObserver()
        {
            ChangeFeedObserver observer = new ObserverExceptionWrappingChangeFeedObserverDecorator(this.observerFactory.CreateObserver());
            if (this.withManualCheckpointing)
            {
                return observer;
            }

            return new AutoCheckpointer(observer);
        }
    }
}