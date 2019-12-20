//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Scripts;
    using Azure.Cosmos.Serialization;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// Cosmos JSON converter. This allows custom JSON parsers.
        /// </summary>
        private readonly CosmosSerializer cosmosSerializer;

        /// <summary>
        /// This is used for all meta data types
        /// </summary>
        private readonly CosmosSerializer propertiesSerializer;

        internal CosmosResponseFactory(
            CosmosSerializer defaultJsonSerializer,
            CosmosSerializer userJsonSerializer)
        {
            this.propertiesSerializer = defaultJsonSerializer;
            this.cosmosSerializer = userJsonSerializer;
        }

        internal async Task<IReadOnlyList<T>> CreateQueryFeedResponseWithPropertySerializerAsync<T>(
            Response cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            FeedResponse<T> feedResponse = await this.CreateQueryFeedResponseHelperAsync<T>(
                cosmosResponseMessage,
                true,
                cancellationToken);

            return feedResponse.Value.ToList().AsReadOnly();
        }

        internal async Task<IReadOnlyList<T>> CreateQueryFeedResponseAsync<T>(
            Response cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            FeedResponse<T> feedResponse = await this.CreateQueryFeedResponseHelperAsync<T>(
                cosmosResponseMessage,
                false,
                cancellationToken);

            return feedResponse.Value.ToList().AsReadOnly();
        }

        private async Task<FeedResponse<T>> CreateQueryFeedResponseHelperAsync<T>(
            Response cosmosResponseMessage,
            bool usePropertySerializer,
            CancellationToken cancellationToken)
        {
            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            // The property serializer should be used for internal
            // query operations like throughput since user serializer can break the logic
            CosmosSerializer serializer = usePropertySerializer ? this.propertiesSerializer : this.cosmosSerializer;

            QueryResponse queryResponse = cosmosResponseMessage as QueryResponse;
            if (queryResponse != null)
            {
                return QueryResponse<T>.CreateResponse<T>(
                    cosmosQueryResponse: queryResponse,
                    jsonSerializer: serializer);
            }

            return await ReadFeedResponse<T>.CreateResponseAsync<T>(
                       cosmosResponseMessage,
                       serializer,
                       cancellationToken);
        }

        internal async Task<ContainerResponse> CreateContainerResponseAsync(
            CosmosContainer container,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                ContainerProperties containerProperties = await CosmosResponseFactory.ToObjectInternalAsync<ContainerProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);

                return new ContainerResponse(
                    cosmosResponseMessage,
                    containerProperties,
                    container);
            });
        }

        internal async Task<DatabaseResponse> CreateDatabaseResponseAsync(
            CosmosDatabase database,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                DatabaseProperties databaseProperties = await CosmosResponseFactory.ToObjectInternalAsync<DatabaseProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);

                return new DatabaseResponse(
                    cosmosResponseMessage,
                    databaseProperties,
                    database);
            });
        }

        internal async Task<ItemResponse<T>> CreateItemResponseAsync<T>(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                T item = await CosmosResponseFactory.ToObjectInternalAsync<T>(cosmosResponseMessage, this.cosmosSerializer, cancellationToken);
                return new ItemResponse<T>(cosmosResponseMessage, item);
            });
        }

        internal async Task<ThroughputResponse> CreateThroughputResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                ThroughputProperties throughputProperties = await CosmosResponseFactory.ToObjectInternalAsync<ThroughputProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);

                return new ThroughputResponse(
                    cosmosResponseMessage,
                    throughputProperties);
            });
        }

        internal async Task<UserResponse> CreateUserResponseAsync(
            CosmosUser user,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                UserProperties userProperties = await CosmosResponseFactory.ToObjectInternalAsync<UserProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);
                return new UserResponse(
                    cosmosResponseMessage,
                    userProperties,
                    user);
            });
        }

        internal async Task<PermissionResponse> CreatePermissionResponseAsync(
            CosmosPermission permission,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                PermissionProperties permissionProperties = await CosmosResponseFactory.ToObjectInternalAsync<PermissionProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);
                return new PermissionResponse(
                    cosmosResponseMessage,
                    permissionProperties,
                    permission);
            });
        }

        internal async Task<StoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponseAsync<T>(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                T item = await CosmosResponseFactory.ToObjectInternalAsync<T>(
                    cosmosResponseMessage,
                    this.cosmosSerializer,
                    cancellationToken);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage,
                    item);
            });
        }

        internal async Task<Response<StoredProcedureProperties>> CreateStoredProcedureResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                StoredProcedureProperties cosmosStoredProcedure = await CosmosResponseFactory.ToObjectInternalAsync<StoredProcedureProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);
                return Response.FromValue(cosmosStoredProcedure, cosmosResponseMessage);
            });
        }

        internal async Task<Response<TriggerProperties>> CreateTriggerResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                TriggerProperties triggerProperties = await CosmosResponseFactory.ToObjectInternalAsync<TriggerProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);
                return Response.FromValue(triggerProperties, cosmosResponseMessage);
            });
        }

        internal async Task<Response<UserDefinedFunctionProperties>> CreateUserDefinedFunctionResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return await this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                UserDefinedFunctionProperties settings = await CosmosResponseFactory.ToObjectInternalAsync<UserDefinedFunctionProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer,
                    cancellationToken);
                return Response.FromValue(settings, cosmosResponseMessage);
            });
        }

        internal async Task<T> ProcessMessageAsync<T>(Task<Response> cosmosResponseTask, Func<Response, Task<T>> createResponse)
        {
            using (Response message = await cosmosResponseTask)
            {
                return await createResponse(message);
            }
        }

        internal static async Task<T> ToObjectInternalAsync<T>(Response response, CosmosSerializer jsonSerializer, CancellationToken cancellationToken)
        {
            //Throw the exception
            response.EnsureSuccessStatusCode();
            if (response.ContentStream == null)
            {
                return default(T);
            }

            return await jsonSerializer.FromStreamAsync<T>(response.ContentStream, cancellationToken);
        }
    }
}