// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal abstract class AvailabilityStrategyInternal : AvailabilityStrategy
    {
        /// <summary>
        /// Execute the availability strategy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="client"></param>
        /// <param name="requestMessage"></param>
        /// <param name="childTrace"></param>
        /// <param name="resourceUriString"></param>
        /// <param name="resourceType"></param>
        /// <param name="operationType"></param>
        /// <param name="requestOptions"></param>
        /// <param name="cosmosContainerCore"></param>
        /// <param name="feedRange"></param>
        /// <param name="streamPayload"></param>
        /// <param name="requestEnricher"></param>
        /// <param name="trace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The response from the service after the availability strategy is executed</returns>
        internal abstract Task<ResponseMessage> ExecuteAvailabilityStrategyAsync(
            Func<RequestMessage,
                ITrace,
                string,
                ResourceType,
                OperationType,
                RequestOptions,
                ContainerInternal,
                FeedRange,
                Stream,
                Action<RequestMessage>,
                ITrace, CancellationToken,
                Task<ResponseMessage>> sender,
            CosmosClient client,
            RequestMessage requestMessage,
            ITrace childTrace,
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            ITrace trace,
            CancellationToken cancellationToken);

        /// <summary>
        /// Checks to see if the strategy is enabled
        /// </summary>
        /// <returns>a bool representing if the strategy is enabled</returns>
        internal abstract bool Enabled();
    }
}
