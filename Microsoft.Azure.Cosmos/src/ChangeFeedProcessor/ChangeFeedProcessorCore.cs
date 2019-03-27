//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement;

    internal sealed class ChangeFeedProcessorCore : ChangeFeedProcessor
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly PartitionManager partitionManager;

        public ChangeFeedProcessorCore(PartitionManager partitionManager)
        {
            if (partitionManager == null) throw new ArgumentNullException(nameof(partitionManager));
            this.partitionManager = partitionManager;
        }

        public override async Task StartAsync()
        {
            Logger.InfoFormat("Starting processor...");
            await this.partitionManager.StartAsync().ConfigureAwait(false);
            Logger.InfoFormat("Processor started.");
        }

        public override async Task StopAsync()
        {
            Logger.InfoFormat("Stopping processor...");
            await this.partitionManager.StopAsync().ConfigureAwait(false);
            Logger.InfoFormat("Processor stopped.");
        }
    }
}