//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;

    internal sealed class AutoCheckpointer : ChangeFeedObserver
    {
        private readonly CheckpointFrequency checkpointFrequency;
        private readonly ChangeFeedObserver observer;
        private long processedBatchCount;
        private DateTime lastCheckpointTime;

        public AutoCheckpointer(CheckpointFrequency checkpointFrequency, ChangeFeedObserver observer)
        {
            if (checkpointFrequency == null)
            {
                throw new ArgumentNullException(nameof(checkpointFrequency));
            }

            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            this.lastCheckpointTime = DateTime.UtcNow;
            this.checkpointFrequency = checkpointFrequency;
            this.observer = observer;
        }

        public override Task OpenAsync(ChangeFeedObserverContext context)
        {
            return this.observer.OpenAsync(context);
        }

        public override Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return this.observer.CloseAsync(context, reason);
        }

        public override async Task ProcessChangesAsync(ChangeFeedObserverContext context, Stream stream, CancellationToken cancellationToken)
        {
            await this.observer.ProcessChangesAsync(context, stream, cancellationToken).ConfigureAwait(false);
            this.processedBatchCount++;
            if (this.IsCheckpointNeeded())
            {
                await context.CheckpointAsync().ConfigureAwait(false);
                this.processedBatchCount = 0;
                this.lastCheckpointTime = DateTime.UtcNow;
            }
        }

        private bool IsCheckpointNeeded()
        {
            if (!this.checkpointFrequency.ProcessedDocumentCount.HasValue && !this.checkpointFrequency.TimeInterval.HasValue)
            {
                return true;
            }

            if (this.processedBatchCount >= this.checkpointFrequency.ProcessedDocumentCount)
            {
                return true;
            }

            TimeSpan delta = DateTime.UtcNow - this.lastCheckpointTime;
            if (delta >= this.checkpointFrequency.TimeInterval)
            {
                return true;
            }

            return false;
        }
    }
}