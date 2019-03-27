//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides an API to start and stop a <see cref="ChangeFeedProcessorCore"/> instance created by <see cref="ChangeFeedProcessorBuilder{T}.BuildAsync"/>.
    /// </summary>
    public abstract class ChangeFeedProcessor
    {
        /// <summary>
        /// Start listening for changes.
        /// </summary>
        /// <returns>A <see cref="Task"/>.</returns>
        public abstract Task StartAsync();

        /// <summary>
        /// Stops listening for changes.
        /// </summary>
        /// <returns>A <see cref="Task"/>.</returns>
        public abstract Task StopAsync();
    }
}