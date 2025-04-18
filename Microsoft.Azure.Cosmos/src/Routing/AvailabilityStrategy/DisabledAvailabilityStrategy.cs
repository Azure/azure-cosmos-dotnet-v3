//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// A Disabled availability strategy that does not do anything. Used for overriding the default global availability strategy.
    /// </summary>
    internal class DisabledAvailabilityStrategy : AvailabilityStrategyInternal
    {
        /// <inheritdoc/>
        internal override bool Enabled()
        {
            return false;
        }

        /// <summary>
        /// This method is not implemented and will throw an exception if called.
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
        /// <returns>nothing, this will throw.</returns>
        internal override Task<ResponseMessage> ExecuteAvailabilityStrategyAsync(
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
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}