//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    using System.Threading;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;

    internal sealed class ChangeFeedEstimatorCore : ChangeFeedProcessor
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly CancellationTokenSource shutdownCts = new CancellationTokenSource();
        private readonly FeedEstimator feedEstimator;
        private Task runAsync;

        public ChangeFeedEstimatorCore(FeedEstimator feedEstimator)
        {
            if (feedEstimator == null) throw new ArgumentNullException(nameof(feedEstimator));
            this.feedEstimator = feedEstimator;
        }

        public override Task StartAsync()
        {
            Logger.InfoFormat("Starting estimator...");
            this.runAsync = this.feedEstimator.RunAsync(this.shutdownCts.Token);
            return Task.CompletedTask;
        }

        public override async Task StopAsync()
        {
            Logger.InfoFormat("Stopping estimator...");
            this.shutdownCts.Cancel();
            try
            {
                await this.runAsync.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Expected during shutdown
            }
        }
    }
}