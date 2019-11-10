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
    using Microsoft.Azure.Documents;

    internal static class ExecUtils
    {
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUriString,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUriString,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: null,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            Stream streamPayload,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUriString,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: streamPayload,
                requestEnricher: null,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Used internally by friends ensrue robust argument and
        /// exception-less handling, with container information
        /// </summary>
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            return ExecUtils.ProcessResourceOperationAsync(
                requestHandler: client.RequestHandler,
                resourceUriString: resourceUriString,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        internal static async Task<T> ProcessResourceOperationAsync<T>(
            RequestInvokerHandler requestHandler,
            string resourceUriString,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            if (requestHandler == null)
            {
                throw new ArgumentException(nameof(requestHandler));
            }

            if (resourceUriString == null)
            {
                throw new ArgumentNullException(nameof(resourceUriString));
            }

            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            ResponseMessage response = await requestHandler.SendAsync(
                resourceUriString,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore,
                partitionKey,
                streamPayload,
                requestEnricher);

            return responseCreator(response);
        }
    }
}