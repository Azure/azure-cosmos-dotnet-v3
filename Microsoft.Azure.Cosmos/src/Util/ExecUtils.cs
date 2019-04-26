//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.Azure.Documents;

    internal static class ExecUtils
    {
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Action<CosmosRequestMessage> requestEnricher,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
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
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
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
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Stream streamPayload,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
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
        internal static async Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CosmosContainerCore cosmosContainerCore,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (client.RequestHandler == null)
            {
                throw new ArgumentException(nameof(client));
            }

            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            CosmosRequestMessage request = await ExecUtils.GenerateCosmosRequestMessage(
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore,
                partitionKey,
                streamPayload,
                requestEnricher);

            CosmosResponseMessage response = await client.RequestHandler.SendAsync(request, cancellationToken);
            return responseCreator(response);
        }

        /// <summary>
        /// Used internally by friends ensrue robust argument and
        /// exception-less handling
        /// </summary>
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ProcessResourceOperationAsync(client: client, resourceUri: resourceUri, resourceType: resourceType, operationType: operationType, requestOptions: requestOptions, cosmosContainerCore: null
                , partitionKey: partitionKey, streamPayload: streamPayload, requestEnricher: requestEnricher, responseCreator: responseCreator, cancellationToken: cancellationToken);
        }

        internal static Task<CosmosResponseMessage> ProcessResourceOperationStreamAsync(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            return ProcessResourceOperationStreamAsync(client, resourceUri, resourceType, operationType, requestOptions,
                null, partitionKey, streamPayload, requestEnricher, cancellationToken);
        }

        internal static async Task<CosmosResponseMessage> ProcessResourceOperationStreamAsync(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CosmosContainerCore cosmosContainerCore,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            CosmosRequestMessage request = await ExecUtils.GenerateCosmosRequestMessage(
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                cosmosContainerCore,
                partitionKey,
                streamPayload,
                requestEnricher);

            return await client.RequestHandler.SendAsync(request, cancellationToken);
        }

        private static async Task<CosmosRequestMessage> GenerateCosmosRequestMessage(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CosmosContainerCore cosmosContainerCore,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher)
        {
            HttpMethod method = ExecUtils.GetHttpMethod(operationType);

            CosmosRequestMessage request = new CosmosRequestMessage(method, resourceUri);
            request.OperationType = operationType;
            request.ResourceType = resourceType;
            request.RequestOptions = requestOptions;
            request.Content = streamPayload;

            if (partitionKey != null)
            {
                if (cosmosContainerCore == null && partitionKey.Equals(PartitionKey.None))
                {
                    throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                }
                else if (partitionKey.Equals(PartitionKey.None))
                {
                    PartitionKeyInternal partitionKeyInternal = await cosmosContainerCore.GetNonePartitionKeyValue();
                    request.Headers.PartitionKey = partitionKeyInternal.ToJsonString();
                }
                else
                {
                    PartitionKey pk = new PartitionKey(partitionKey);
                    request.Headers.PartitionKey = pk.InternalKey.ToJsonString();
                }
            }

            if (operationType == OperationType.Upsert)
            {
                request.Headers.IsUpsert = bool.TrueString;
            }

            requestEnricher?.Invoke(request);

            return request;
        }

        internal static HttpMethod GetHttpMethod(
            OperationType operationType)
        {
            HttpMethod httpMethod = HttpMethod.Head;
            if (operationType == OperationType.Create ||
                operationType == OperationType.Upsert ||
                operationType == OperationType.Query ||
                operationType == OperationType.SqlQuery ||
                operationType == OperationType.Batch ||
                operationType == OperationType.ExecuteJavaScript)
            {
                return HttpMethod.Post;
            }
            else if (operationType == OperationType.Read ||
                operationType == OperationType.ReadFeed)
            {
                return HttpMethod.Get;
            }
            else if (operationType == OperationType.Replace)
            {
                return HttpMethod.Put;
            }
            else if (operationType == OperationType.Delete)
            {
                return HttpMethod.Delete;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
