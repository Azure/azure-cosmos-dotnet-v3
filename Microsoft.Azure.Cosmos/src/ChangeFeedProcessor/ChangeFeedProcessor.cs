//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos
#else
namespace Microsoft.Azure.Cosmos
#endif
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides an API to start and stop a <see cref="ChangeFeedProcessor"/> instance created by <see cref="ChangeFeedProcessorBuilder.Build"/>.
    /// </summary>
    #if AZURECORE
    internal
#else
    public
#endif
    abstract class ChangeFeedProcessor
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