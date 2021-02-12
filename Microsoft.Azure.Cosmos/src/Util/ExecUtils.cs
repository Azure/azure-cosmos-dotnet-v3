//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal static class ExecUtils
    {
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore: null,
                feedRange: null,
                streamPayload: null,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore: null,
                feedRange: null,
                streamPayload: null,
                requestEnricher: null,
                responseCreator: responseCreator,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            Stream streamPayload,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore: null,
                feedRange: null,
                streamPayload: streamPayload,
                requestEnricher: null,
                responseCreator: responseCreator,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Used internally by friends ensrue robust argument and
        /// exception-less handling, with container information
        /// </summary>
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            return ExecUtils.ProcessResourceOperationAsync(
                requestHandler: client.RequestHandler,
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                feedRange: feedRange,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal static async Task<T> ProcessResourceOperationAsync<T>(
            RequestInvokerHandler requestHandler,
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (requestHandler == null)
            {
                throw new ArgumentException(nameof(requestHandler));
            }

            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            ResponseMessage response = await requestHandler.SendAsync(
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore,
                feedRange,
                streamPayload,
                requestEnricher,
                trace,
                cancellationToken);

            return responseCreator(response);
        }
    }
}