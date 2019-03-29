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

        public ChangeFeedEstimatorCore(FeedEstimator feedEstimator)
        {
            if (feedEstimator == null) throw new ArgumentNullException(nameof(feedEstimator));
            this.feedEstimator = feedEstimator;
        }

        public override async Task StartAsync()
        {
            Logger.InfoFormat("Starting estimator...");
            await this.feedEstimator.RunAsync(this.shutdownCts.Token);
        }

        public override Task StopAsync()
        {
            Logger.InfoFormat("Stopping estimator...");
            this.shutdownCts.Cancel();
            return Task.CompletedTask;
        }
    }
}