// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class AvailabilityStrategyInternal : AvailabilityStrategy
    {
        /// <summary>
        /// Execute the availability strategy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="client"></param>
        /// <param name="requestMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The response from the service after the availability strategy is executed</returns>
        internal abstract Task<ResponseMessage> ExecuteAvailabilityStrategyAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CosmosClient client,
            RequestMessage requestMessage,
            CancellationToken cancellationToken);

        /// <summary>
        /// Checks to see if the strategy is enabled
        /// </summary>
        /// <returns>a bool representing if the strategy is enabled</returns>
        internal abstract bool Enabled();
    }
}
